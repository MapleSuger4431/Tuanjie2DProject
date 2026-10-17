using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>UR038: game declarations may not shadow public framework event types.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FrameworkEventShadowAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor UR038 = new(
            "UR038",
            "Framework event type shadowed",
            "Game type '{0}' shadows public framework event type 'Create.GameFramework.{0}'; use the framework event or choose a distinct name",
            "Architecture",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UR038);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var names = GetFrameworkEventNames(start.Compilation);
                if (names.Count == 0) return;
                start.RegisterSyntaxNodeAction(c => AnalyzeDeclaration(c, names),
                    SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
            });
        }

        private static HashSet<string> GetFrameworkEventNames(Compilation compilation)
        {
            var names = new HashSet<string>(System.StringComparer.Ordinal);
            var create = FindNamespace(compilation.GlobalNamespace, "Create");
            var framework = create == null ? null : FindNamespace(create, "GameFramework");
            if (framework == null) return names;

            foreach (var type in framework.GetTypeMembers())
            {
                if (type.DeclaredAccessibility == Accessibility.Public &&
                    (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
                    type.Name.EndsWith("Event", System.StringComparison.Ordinal))
                {
                    names.Add(type.Name);
                }
            }
            return names;
        }

        private static INamespaceSymbol? FindNamespace(INamespaceSymbol parent, string name)
        {
            foreach (var child in parent.GetNamespaceMembers())
                if (child.Name == name) return child;
            return null;
        }

        private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context, HashSet<string> eventNames)
        {
            var declaration = (TypeDeclarationSyntax)context.Node;
            if (!eventNames.Contains(declaration.Identifier.ValueText)) return;
            var symbol = context.SemanticModel.GetDeclaredSymbol(declaration);
            if (symbol == null || symbol.ContainingType != null) return;
            if (symbol.ContainingNamespace.ToDisplayString() == "Create.GameFramework") return;
            context.ReportDiagnostic(Diagnostic.Create(UR038, declaration.Identifier.GetLocation(), symbol.Name));
        }
    }
}
