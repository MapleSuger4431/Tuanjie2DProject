using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>
    /// UR029: 禁止使用 Resources 加载 API（Resources.Load / LoadAsync / LoadAll / GetBuiltinResource 等）。
    ///        本项目禁用 Resources/ 文件夹，所有资产通过直接引用加载。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ResourcesLoadAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor UR029 = new(
            id: "UR029",
            title: "Do not use UnityEngine.Resources loading APIs",
            messageFormat: "Forbidden Resources API 'Resources.{0}'. The Resources/ folder is banned in this project; load assets via direct references (SerializeField, Addressables, or explicit asset references) instead.",
            category: "Correctness",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly HashSet<string> ForbiddenMethods = new()
        {
            "Load",
            "LoadAsync",
            "LoadAll",
            "GetBuiltinResource",
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(UR029);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;

            var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol != null)
            {
                // 语义可用时精确判定：UnityEngine.Resources 上的禁用方法。
                if (ForbiddenMethods.Contains(symbol.Name) &&
                    symbol.ContainingType?.Name == "Resources" &&
                    IsUnityEngineType(symbol.ContainingType))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(UR029, invocation.GetLocation(), symbol.Name));
                }
                return;
            }

            // 语义未解析时（引用缺失等）回退到语法判定：`Resources.Method(...)`。
            if (invocation.Expression is MemberAccessExpressionSyntax ma &&
                ForbiddenMethods.Contains(ma.Name.Identifier.ValueText))
            {
                string? recv = ma.Expression switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    MemberAccessExpressionSyntax inner => inner.Name.Identifier.ValueText,
                    _ => null
                };
                if (recv == "Resources")
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(UR029, invocation.GetLocation(), ma.Name.Identifier.ValueText));
                }
            }
        }

        private static bool IsUnityEngineType(ITypeSymbol type)
        {
            var ns = type.ContainingNamespace?.ToDisplayString();
            return ns != null && ns.StartsWith("UnityEngine");
        }
    }
}
