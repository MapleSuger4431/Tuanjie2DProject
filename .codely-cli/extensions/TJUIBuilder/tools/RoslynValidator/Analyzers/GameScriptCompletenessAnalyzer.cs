using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>Hard-gate completeness checks used only by the game-scripts profile.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GameScriptCompletenessAnalyzer : DiagnosticAnalyzer
    {
        private static DiagnosticDescriptor Rule(string id, string title, string message) =>
            new(id, title, message, "Completeness", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UR032 = Rule("UR032", "NotImplementedException stub", "'{0}' throws NotImplementedException");
        public static readonly DiagnosticDescriptor UR033 = Rule("UR033", "Comment-only placeholder method", "Method '{0}' contains a placeholder comment but no effective statement");
        public static readonly DiagnosticDescriptor UR034 = Rule("UR034", "Placeholder default getter", "Property '{0}' has a placeholder getter returning default/constant data");
        public static readonly DiagnosticDescriptor UR035 = Rule("UR035", "Empty core method", "Core method '{0}' has an empty placeholder implementation");
        public static readonly DiagnosticDescriptor UR036 = Rule("UR036", "TODO exception stub", "'{0}' throws NotSupportedException/Exception as a TODO placeholder stub");

        private static readonly string[] LegalEmptyUnityHooks =
        {
            "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "Update", "LateUpdate", "FixedUpdate",
            "Reset", "OnValidate", "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UR032, UR033, UR034, UR035, UR036);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            if (method.Body == null || method.Modifiers.Any(SyntaxKind.AbstractKeyword) ||
                method.Parent is InterfaceDeclarationSyntax) return;

            var throws = method.Body.DescendantNodes().OfType<ThrowStatementSyntax>().ToArray();
            foreach (var statement in throws)
            {
                var type = ExceptionType(statement.Expression);
                if (type == "NotImplementedException")
                {
                    context.ReportDiagnostic(Diagnostic.Create(UR032, statement.GetLocation(), method.Identifier.ValueText));
                }
                else if ((type == "NotSupportedException" || type == "Exception") &&
                         PlaceholderReturnAnalyzer.HasPlaceholderComment(method))
                {
                    context.ReportDiagnostic(Diagnostic.Create(UR036, statement.GetLocation(), method.Identifier.ValueText));
                }
            }

            if (!PlaceholderReturnAnalyzer.HasPlaceholderComment(method)) return;
            if (method.Body.Statements.Count == 0)
            {
                var rule = LegalEmptyUnityHooks.Contains(method.Identifier.ValueText, StringComparer.Ordinal) ? null :
                    (LooksCore(method.Identifier.ValueText) ? UR035 : UR033);
                if (rule != null) context.ReportDiagnostic(Diagnostic.Create(rule, method.Identifier.GetLocation(), method.Identifier.ValueText));
            }
        }

        private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;
            if (!PlaceholderReturnAnalyzer.HasPlaceholderComment(property)) return;
            var getter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
            var expression = getter?.ExpressionBody?.Expression ??
                getter?.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault()?.Expression ??
                property.ExpressionBody?.Expression;
            if (PlaceholderReturnAnalyzer.IsConstant(expression))
                context.ReportDiagnostic(Diagnostic.Create(UR034, property.Identifier.GetLocation(), property.Identifier.ValueText));
        }

        private static string? ExceptionType(ExpressionSyntax? expression)
        {
            if (expression is not ObjectCreationExpressionSyntax creation) return null;
            return creation.Type switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                _ => creation.Type.ToString().Split('.').Last()
            };
        }

        private static bool LooksCore(string name)
        {
            var verbs = new[] { "Calculate", "Compute", "Get", "Set", "Apply", "Handle", "Spawn", "Move", "Attack", "Save", "Load", "Initialize", "ResetGame" };
            return verbs.Any(v => name.StartsWith(v, StringComparison.Ordinal));
        }
    }
}
