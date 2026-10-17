using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>
    /// UR028: if/else-if 链中较晚分支的条件"蕴含"较早分支——较早分支的合取子句是较晚分支的子集，
    /// 则 else-if 仅在前序分支为假时才进入，故此分支恒为假、内部逻辑是死代码。
    ///
    /// Roslyn 版用语法树的
    /// IfStatementSyntax + Else/if 链，条件比原实现更准（AST 已切好条件表达式，无需正则找括号）。
    /// 判据与原实现一致：仅处理纯合取条件（不含 ||），把条件按顶层 && 拆成子句集合，
    /// 若较早分支的子句集合 ⊆ 较晚分支的子句集合 → 较晚分支不可达。行含 SuppressWiringCheck 豁免。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnreachableBranchAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor UR028 = new(
            id: "UR028",
            title: "Unreachable else-if branch (implies an earlier branch)",
            messageFormat: "Branch condition \"{0}\" is unreachable because it implies the earlier branch \"{1}\" in the same if/else chain (else-if only runs when earlier branches are false), so its body is dead code",
            category: "Correctness",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(UR028);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
        }

        private static void AnalyzeIf(SyntaxNodeAnalysisContext ctx)
        {
            var ifStmt = (IfStatementSyntax)ctx.Node;

            // 只从链首处理（父不是 else 子句），避免对同一条链重复分析。
            if (ifStmt.Parent is ElseClauseSyntax) return;

            // 收集整条 if / else-if 链的条件（else 无条件的忽略）。
            var chain = new List<IfStatementSyntax>();
            var cur = ifStmt;
            while (cur != null)
            {
                chain.Add(cur);
                cur = cur.Else?.Statement as IfStatementSyntax;
            }
            if (chain.Count < 2) return;

            // 预拆各分支条件的顶层合取子句集合（含 || 的分支标记为 null → 跳过）。
            var conjSets = chain.Select(c => SplitTopLevelAnd(c.Condition)).ToList();

            for (int j = 1; j < chain.Count; j++)
            {
                var cjSet = conjSets[j];
                if (cjSet == null) continue; // 含 ||，不判定
                if (IsLineSuppressed(chain[j])) continue;

                for (int i = 0; i < j; i++)
                {
                    var ciSet = conjSets[i];
                    if (ciSet == null) continue;
                    // 较早分支子句 ⊆ 较晚分支子句 → 较晚分支蕴含较早分支 → 不可达。
                    if (ciSet.Count <= cjSet.Count && ciSet.All(c => cjSet.Contains(c)))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            UR028, chain[j].Condition.GetLocation(),
                            Normalize(chain[j].Condition), Normalize(chain[i].Condition)));
                        break;
                    }
                }
            }
        }

        // 把条件按顶层 && 拆成规范化子句集合；含 || 返回 null（不判定）。
        private static HashSet<string>? SplitTopLevelAnd(ExpressionSyntax condition)
        {
            var expr = Unwrap(condition);
            if (ContainsTopLevelOr(expr)) return null;

            var parts = new HashSet<string>();
            CollectAndOperands(expr, parts);
            return parts;
        }

        private static void CollectAndOperands(ExpressionSyntax expr, HashSet<string> into)
        {
            expr = Unwrap(expr);
            if (expr is BinaryExpressionSyntax be && be.IsKind(SyntaxKind.LogicalAndExpression))
            {
                CollectAndOperands(be.Left, into);
                CollectAndOperands(be.Right, into);
            }
            else
            {
                into.Add(Normalize(expr));
            }
        }

        private static bool ContainsTopLevelOr(ExpressionSyntax expr)
        {
            expr = Unwrap(expr);
            if (expr is BinaryExpressionSyntax be)
            {
                if (be.IsKind(SyntaxKind.LogicalOrExpression)) return true;
                if (be.IsKind(SyntaxKind.LogicalAndExpression))
                    return ContainsTopLevelOr(be.Left) || ContainsTopLevelOr(be.Right);
            }
            return false;
        }

        private static ExpressionSyntax Unwrap(ExpressionSyntax expr)
        {
            while (expr is ParenthesizedExpressionSyntax p) expr = p.Expression;
            return expr;
        }

        // 规范化：去空白，作为子句相等的判据（与原实现"文本比较"同精神，但更稳）。
        private static string Normalize(ExpressionSyntax expr)
        {
            return string.Concat(Unwrap(expr).ToString().Where(c => !char.IsWhiteSpace(c)));
        }

        private static bool IsLineSuppressed(SyntaxNode node)
        {
            var text = node.SyntaxTree.GetText();
            var line = text.Lines[node.GetLocation().GetLineSpan().StartLinePosition.Line];
            return line.ToString().Contains("SuppressWiringCheck");
        }
    }
}
