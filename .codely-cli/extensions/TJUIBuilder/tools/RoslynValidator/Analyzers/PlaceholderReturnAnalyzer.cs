using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>UR025: placeholder comment plus an unconditional constant implementation.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PlaceholderReturnAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor UR025 = new(
            "UR025",
            "Placeholder method returning a constant",
            "Method '{0}' has a placeholder comment and an unconditional constant implementation; implement the real logic",
            "Correctness",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly string[] Keywords =
        {
            "占位", "TODO", "FIXME", "placeholder", "临时", "待补", "需补充",
            "2b 阶段", "2b阶段", "先返回", "暂时"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(UR025);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
        }

        private static void Analyze(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            if (method.Body == null || !HasPlaceholderComment(method)) return;
            if (HasStructuredExemption(method, "UR025")) return;

            // A guard return plus real work is not a constant implementation. The whole body must
            // consist of exactly one return statement whose expression is constant.
            if (method.Body.Statements.Count != 1 ||
                method.Body.Statements[0] is not ReturnStatementSyntax ret ||
                !IsConstant(ret.Expression)) return;

            context.ReportDiagnostic(Diagnostic.Create(UR025, method.Identifier.GetLocation(), method.Identifier.ValueText));
        }

        internal static bool HasPlaceholderComment(SyntaxNode node) => node.GetLeadingTrivia()
            .Concat(node.DescendantTrivia(descendIntoTrivia: true))
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                        t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .Any(t => Keywords.Any(k => t.ToString().IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));

        private static bool HasStructuredExemption(SyntaxNode node, string rule) => node.DescendantTrivia(descendIntoTrivia: true)
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .Any(t =>
            {
                var text = t.ToString();
                return text.IndexOf("SuppressWiringCheck", StringComparison.Ordinal) >= 0 &&
                       text.IndexOf(rule, StringComparison.OrdinalIgnoreCase) >= 0 &&
                       text.IndexOf("reason:", StringComparison.OrdinalIgnoreCase) >= 0 &&
                       text.Split(new[] { "reason:" }, StringSplitOptions.None).Last().Trim(' ', '/', '*').Length > 0;
            });

        internal static bool IsConstant(ExpressionSyntax? expression)
        {
            if (expression == null) return false;
            if (expression.IsKind(SyntaxKind.NumericLiteralExpression) || expression.IsKind(SyntaxKind.StringLiteralExpression) ||
                expression.IsKind(SyntaxKind.CharacterLiteralExpression) || expression.IsKind(SyntaxKind.TrueLiteralExpression) ||
                expression.IsKind(SyntaxKind.FalseLiteralExpression) || expression.IsKind(SyntaxKind.NullLiteralExpression) ||
                expression.IsKind(SyntaxKind.DefaultLiteralExpression) || expression is DefaultExpressionSyntax) return true;
            return expression is PrefixUnaryExpressionSyntax prefix &&
                   (prefix.IsKind(SyntaxKind.UnaryMinusExpression) || prefix.IsKind(SyntaxKind.UnaryPlusExpression)) &&
                   prefix.Operand.IsKind(SyntaxKind.NumericLiteralExpression);
        }
    }
}
