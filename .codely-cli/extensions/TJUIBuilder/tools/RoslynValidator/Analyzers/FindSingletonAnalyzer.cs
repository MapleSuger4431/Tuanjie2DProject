using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>
    /// UR030: 框架单例不应用 FindObjectOfType 运行时查找，应直接用 MonoSingleton&lt;T&gt;.Instance。
    ///
    /// 背景：游戏侧框架（游戏编排 extension）可提供 MonoSingleton&lt;T&gt; 作为单例基类
    /// （管理器/配置型 bootstrap 继承它）。用 FindObjectOfType&lt;T&gt;() 去拿这些本就是单例的
    /// 实例，属于"框架已提供、却手写等价查找"的反模式：
    ///   • 每次调用全场景遍历，比 O(1) 的 .Instance 慢；
    ///   • 绕开了框架单例的生命周期/唯一性保证；
    ///   • Editor API FindObjectOfType 在运行时语义上也不如 .Instance 直观。
    ///
    /// 注意：本 UI 包（cn.tuanjie.create.ui）已不内置 MonoSingleton&lt;T&gt;；本规则按语义
    /// 识别 Create.GameFramework.MonoSingleton&lt;&gt; 派生类型，仅供游戏侧重新引入该基类时生效。
    ///
    /// 只对"泛型实参 T 的基类链里确实含 MonoSingleton&lt;&gt;"的调用报警——
    /// 因此 FindObjectOfType&lt;SomeSceneObject&gt;()（非单例类型）等合法用法不误伤。
    ///
    /// 抓的模式（UnityEngine.Object 上的静态查找方法，泛型形式）：
    ///   • FindObjectOfType&lt;T&gt;() / FindObjectsOfType&lt;T&gt;()
    ///   • FindAnyObjectByType&lt;T&gt;() / FindFirstObjectByType&lt;T&gt;()
    /// 当 T : MonoSingleton&lt;...&gt; 时报 Warning。
    ///
    /// 行级豁免：所在行含 "SuppressWiringCheck" 注释则跳过（与 UR021 等一致）。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FindSingletonAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor UR030 = new(
            id: "UR030",
            title: "Do not FindObjectOfType a framework MonoSingleton",
            messageFormat: "'{0}<{1}>' looks up a framework MonoSingleton; use 'MonoSingleton<{1}>.Instance' (or '{1}.Instance', guarded by MonoSingleton<{1}>.HasInstance) instead of a runtime scene scan",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // Unity 的对象查找方法（泛型重载）。
        private static readonly HashSet<string> FindMethods = new()
        {
            "FindObjectOfType",
            "FindObjectsOfType",
            "FindAnyObjectByType",
            "FindFirstObjectByType",
            "FindObjectsByType",
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(UR030);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;
            if (IsLineSuppressed(invocation)) return;

            // 取被调用方法名（支持 FindObjectOfType<T>() 与 obj.FindObjectOfType<T>() 两种形态）。
            string? methodName = invocation.Expression switch
            {
                GenericNameSyntax g => g.Identifier.ValueText,
                MemberAccessExpressionSyntax ma when ma.Name is GenericNameSyntax mg => mg.Identifier.ValueText,
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax ma2 => ma2.Name.Identifier.ValueText,
                _ => null
            };
            if (methodName == null || !FindMethods.Contains(methodName)) return;

            // 优先用语义模型拿泛型实参 T；语义可用时同时校验方法确来自 UnityEngine.Object。
            var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            ITypeSymbol? typeArg = null;

            if (symbol != null)
            {
                if (!IsUnityObjectFindMethod(symbol, ctx.Compilation)) return;
                if (symbol.TypeArguments.Length == 1)
                    typeArg = symbol.TypeArguments[0];
            }

            // 语义拿不到实参（引用缺失等）时，回退用语法里的泛型实参解析类型。
            if (typeArg == null)
            {
                var genericName = invocation.Expression switch
                {
                    GenericNameSyntax g => g,
                    MemberAccessExpressionSyntax ma when ma.Name is GenericNameSyntax mg => mg,
                    _ => null
                };
                if (genericName == null || genericName.TypeArgumentList.Arguments.Count != 1) return;
                typeArg = ctx.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
            }

            if (typeArg is not INamedTypeSymbol named) return;

            // 只有当 T 的基类链里含 MonoSingleton<...> 才报警。
            if (!InheritsMonoSingleton(named)) return;

            ctx.ReportDiagnostic(Diagnostic.Create(
                UR030, invocation.GetLocation(), methodName, named.Name));
        }

        // 方法是否为 UnityEngine.Object（或其派生）上的对象查找方法。
        private static bool IsUnityObjectFindMethod(IMethodSymbol method, Compilation compilation)
        {
            var unityObj = compilation.GetTypeByMetadataName("UnityEngine.Object");
            if (unityObj == null) return true; // 引用缺失时不因此漏报，交由名字匹配兜底
            var container = method.ContainingType;
            while (container != null)
            {
                if (SymbolEqualityComparer.Default.Equals(container, unityObj)) return true;
                container = container.BaseType;
            }
            return false;
        }

        // 沿基类链判断类型是否派生自框架 MonoSingleton<>（按原始定义比对，忽略具体泛型实参）。
        private static bool InheritsMonoSingleton(INamedTypeSymbol type)
        {
            var t = type.BaseType;
            while (t != null)
            {
                if (t.IsGenericType &&
                    t.OriginalDefinition?.Name == "MonoSingleton" &&
                    IsCreateGameFramework(t.OriginalDefinition))
                {
                    return true;
                }
                t = t.BaseType;
            }
            return false;
        }

        // 收敛到框架命名空间，避免与同名的用户类型混淆。
        private static bool IsCreateGameFramework(INamedTypeSymbol type)
        {
            var ns = type.ContainingNamespace?.ToDisplayString();
            // MonoSingleton 定义在 Create.GameFramework；命名空间拿不到时（语义不全）放行按名字判定。
            return ns == null || ns.StartsWith("Create.GameFramework") || ns.StartsWith("Create.");
        }

        // 行内 // SuppressWiringCheck 豁免。
        private static bool IsLineSuppressed(SyntaxNode node)
        {
            var text = node.SyntaxTree.GetText();
            var line = text.Lines[node.GetLocation().GetLineSpan().StartLinePosition.Line];
            return line.ToString().Contains("SuppressWiringCheck");
        }
    }
}
