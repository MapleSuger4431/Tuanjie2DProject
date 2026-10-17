using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RoslynValidator.Analyzers
{
    /// <summary>
    /// 属性数据流检测（语义级，跨编译单元统计读/写）：
    ///
    /// UR026: public { get; set; } 自动属性被**读取**却全工程从未被**赋值**——属性不被序列化（Inspector 改不了），
    ///        将永远停在默认值。（迁移自 CheckWriteNeverField）
    /// UR027: public { get; private set; } / { get; protected set; } 自动属性被内部**赋值**却全工程从未被**读取**——
    ///        "算了没人用"的死输出，相关玩法静默失效。（迁移自 CheckUnreadComputedProperty）
    ///
    /// 相比原正则跨文件计数，本分析器用 IOperation 精确区分读/写（含复合赋值 X+=... 视为既读又写、
    /// 自赋值 X=Clamp(X) 的右值读取），near-zero 误报。
    ///
    /// ⚠️ 精度依赖"编译看到全部引用方"：RoslynValidator 在多 agent 2a 阶段可能只传入本批文件，
    /// 此时跨文件的读/写看不到 → 可能误报。故本组规则默认随 --rules 启用，误报用声明行
    /// // SuppressWiringCheck 豁免；全量场景（2b 后）最准。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PropertyDataFlowAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor UR026 = new(
            id: "UR026",
            title: "public { get; set; } property read but never assigned",
            messageFormat: "Property '{0}' {{ get; set; }} is read but never assigned anywhere; auto-properties are not serialized (not editable in Inspector), so it stays at its default value forever",
            category: "Correctness",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor UR027 = new(
            id: "UR027",
            title: "public { get; private set; } property assigned but never read",
            messageFormat: "Property '{0}' {{ get; private set; }} is computed/assigned but never read anywhere, a dead output whose computation is wasted and whose gameplay effect silently never happens",
            category: "Correctness",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(UR026, UR027);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startCtx =>
            {
                // 候选属性：public 自动属性。kind: 1 = { get; set; } (UR026), 2 = { get; private/protected set; } (UR027)
                var candidates = new ConcurrentDictionary<IPropertySymbol, int>(SymbolEqualityComparer.Default);
                var readSet = new ConcurrentDictionary<IPropertySymbol, byte>(SymbolEqualityComparer.Default);
                var writeSet = new ConcurrentDictionary<IPropertySymbol, byte>(SymbolEqualityComparer.Default);

                // 收集候选属性（声明层）。
                startCtx.RegisterSymbolAction(symCtx =>
                {
                    var prop = (IPropertySymbol)symCtx.Symbol;
                    if (prop.DeclaredAccessibility != Accessibility.Public) return;
                    if (prop.IsStatic || prop.IsIndexer) return;
                    if (prop.GetMethod == null || prop.SetMethod == null) return; // 需 get+set
                    if (!IsAutoProperty(prop, symCtx.CancellationToken)) return;   // 排除表达式体/带 body 属性
                    if (IsDeclLineSuppressed(prop, symCtx.CancellationToken)) return;

                    var setAcc = prop.SetMethod.DeclaredAccessibility;
                    if (setAcc == Accessibility.Public)
                        candidates[prop] = 1; // UR026: { get; set; }
                    else if (setAcc == Accessibility.Private || setAcc == Accessibility.Protected
                             || setAcc == Accessibility.ProtectedOrInternal || setAcc == Accessibility.ProtectedAndInternal)
                        candidates[prop] = 2; // UR027: { get; private/protected set; }
                }, SymbolKind.Property);

                // 统计读/写（操作层，语义精确）。
                startCtx.RegisterOperationAction(opCtx =>
                {
                    var propRef = (IPropertyReferenceOperation)opCtx.Operation;
                    var prop = propRef.Property?.OriginalDefinition as IPropertySymbol;
                    if (prop == null) return;

                    var parent = propRef.Parent;
                    // 简单赋值目标：纯写。
                    if (parent is ISimpleAssignmentOperation simple && ReferenceEquals(simple.Target, propRef))
                    {
                        writeSet[prop] = 1;
                        return;
                    }
                    // 复合赋值目标（X += ...）：既读又写。
                    if (parent is ICompoundAssignmentOperation comp && ReferenceEquals(comp.Target, propRef))
                    {
                        writeSet[prop] = 1;
                        readSet[prop] = 1;
                        return;
                    }
                    // 自增自减：既读又写。
                    if (parent is IIncrementOrDecrementOperation)
                    {
                        writeSet[prop] = 1;
                        readSet[prop] = 1;
                        return;
                    }
                    // 其余（RHS/条件/参数/return/初始化等）：读。
                    readSet[prop] = 1;
                }, OperationKind.PropertyReference);

                startCtx.RegisterCompilationEndAction(endCtx =>
                {
                    foreach (var kv in candidates)
                    {
                        var prop = kv.Key;
                        int kind = kv.Value;
                        bool wasRead = readSet.ContainsKey(prop);
                        bool wasWritten = writeSet.ContainsKey(prop);
                        var loc = prop.Locations.FirstOrDefault();
                        if (loc == null || !loc.IsInSource) continue;

                        if (kind == 1 && wasRead && !wasWritten)
                            endCtx.ReportDiagnostic(Diagnostic.Create(UR026, loc, prop.Name));
                        else if (kind == 2 && wasWritten && !wasRead)
                            endCtx.ReportDiagnostic(Diagnostic.Create(UR027, loc, prop.Name));
                    }
                });
            });
        }

        // 自动属性：accessor 无 body、无表达式体。
        private static bool IsAutoProperty(IPropertySymbol prop, System.Threading.CancellationToken ct)
        {
            foreach (var reference in prop.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(ct) is not PropertyDeclarationSyntax decl) continue;
                if (decl.ExpressionBody != null) return false; // => 表达式体属性
                if (decl.AccessorList == null) return false;
                foreach (var acc in decl.AccessorList.Accessors)
                    if (acc.Body != null || acc.ExpressionBody != null) return false; // 有实现体
                return true;
            }
            return false;
        }

        private static bool IsDeclLineSuppressed(IPropertySymbol prop, System.Threading.CancellationToken ct)
        {
            var loc = prop.Locations.FirstOrDefault();
            if (loc == null || !loc.IsInSource) return false;
            var line = loc.SourceTree!.GetText(ct).Lines[loc.GetLineSpan().StartLinePosition.Line];
            return line.ToString().Contains("SuppressWiringCheck");
        }
    }
}
