using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>
    /// UR048 family: component lookups whose result is dereferenced without verification.
    ///
    /// Narrow by design. Error is reserved for high-confidence origins where the component is
    /// known to come from a freshly instantiated prefab, i.e. exactly the cases where a
    /// missing component contract produces a runtime NullReferenceException on the player's first
    /// action. Everything else stays advisory so the rule can be enabled project-wide without
    /// blocking normal development.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ComponentLookupNullAnalyzer : DiagnosticAnalyzer
    {
        private const string UnityObject = "UnityEngine.Object";

        private const string FixAdvice =
            "fix the prefab's component contract so the component is guaranteed, or verify with TryGetComponent / ?. / a null guard that throws a configuration exception naming the prefab and the component — do not silently return";

        public static readonly DiagnosticDescriptor UR048 = Error("UR048",
            "Instantiated component dereferenced without verification",
            "Component '{0}' obtained from {1} is dereferenced without verification; the prefab may not carry it — " + FixAdvice);

        public static readonly DiagnosticDescriptor UR049 = Warning("UR049",
            "Component lookup dereferenced without verification",
            "Component '{0}' obtained from {1} is dereferenced without verification — " + FixAdvice);

        public static readonly DiagnosticDescriptor UR050 = Warning("UR050",
            "Null guard hides a missing component",
            "The null guard for '{0}' exits without surfacing a configuration error, turning a missing component into a silent functional failure — " + FixAdvice);

        private static DiagnosticDescriptor Error(string id, string title, string message) =>
            new(id, title, message, "Reliability", DiagnosticSeverity.Error, true);

        private static DiagnosticDescriptor Warning(string id, string title, string message) =>
            new(id, title, message, "Reliability", DiagnosticSeverity.Warning, true);

        /// <summary>Narrow reasoned suppression; a marker without a non-empty reason does not suppress.</summary>
        private static readonly Regex ExemptionMarker = new(
            @"ComponentLookupExemption:\s*reason=(?<reason>\S[^\r\n]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly ImmutableHashSet<string> LookupMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal, "GetComponent", "GetComponentInChildren", "GetComponentInParent");

        /// <summary>Where the looked-up component instance came from.</summary>
        private enum Origin
        {
            /// <summary>Same GameObject and guaranteed by [RequireComponent]; never reported.</summary>
            GuaranteedSameObject,
            /// <summary>Object.Instantiate(prefab) — high confidence, Error.</summary>
            Instantiate,
            /// <summary>Anything else — advisory, Warning.</summary>
            Other,
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UR048, UR049, UR050);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
                return;

            if (!LookupMethods.Contains(method.Name)) return;

            INamedTypeSymbol? component = ResolveComponentType(invocation, method, context.SemanticModel, context.CancellationToken);
            if (component == null) return;

            Origin origin = ClassifyOrigin(invocation, context.SemanticModel, component, context.CancellationToken);
            if (origin == Origin.GuaranteedSameObject) return;

            Location? dereference = FindUnverifiedDereference(invocation, context, out bool guardIsSilent, out Location? guard);
            if (guardIsSilent && guard != null && !IsSuppressed(invocation, guard))
                context.ReportDiagnostic(Diagnostic.Create(UR050, guard, component.Name));
            if (dereference == null || IsSuppressed(invocation, dereference)) return;

            (DiagnosticDescriptor rule, string source) = origin switch
            {
                Origin.Instantiate => (UR048, "Object.Instantiate()"),
                _ => (UR049, "a cross-object component lookup"),
            };
            context.ReportDiagnostic(Diagnostic.Create(rule, dereference, component.Name, source));
        }

        // ── origin classification ────────────────────────────────────────────────

        private static bool IsInstantiate(IMethodSymbol method) =>
            method.Name == "Instantiate" && method.IsStatic &&
            HasFullyQualifiedName(method.ContainingType, UnityObject);

        private static bool HasFullyQualifiedName(INamedTypeSymbol? type, string expected) =>
            type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == expected;

        private static INamedTypeSymbol? ResolveComponentType(InvocationExpressionSyntax invocation,
            IMethodSymbol method, SemanticModel model, System.Threading.CancellationToken cancellationToken)
        {
            if (method.TypeArguments.Length == 1)
                return method.TypeArguments[0] as INamedTypeSymbol;
            // Non-generic GetComponent(typeof(T)) / GetComponent("Name") is not narrow enough to gate on.
            return null;
        }

        private static Origin ClassifyOrigin(InvocationExpressionSyntax invocation, SemanticModel model,
            INamedTypeSymbol component, System.Threading.CancellationToken cancellationToken)
        {
            ExpressionSyntax? receiver = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Expression,
                MemberBindingExpressionSyntax => null,
                _ => null,
            };

            if (receiver == null || receiver is ThisExpressionSyntax)
                return IsGuaranteedByRequireComponent(invocation, model, component, cancellationToken)
                    ? Origin.GuaranteedSameObject
                    : Origin.Other;

            return ClassifyExpression(receiver, model, cancellationToken, hops: 0);
        }

        private static Origin ClassifyExpression(ExpressionSyntax expression, SemanticModel model,
            System.Threading.CancellationToken cancellationToken, int hops)
        {
            expression = Unwrap(expression);

            if (expression is InvocationExpressionSyntax call &&
                model.GetSymbolInfo(call, cancellationToken).Symbol is IMethodSymbol called)
            {
                if (IsInstantiate(called)) return Origin.Instantiate;
                return Origin.Other;
            }

            // One-hop-at-a-time local initializer tracing, confined to a single method body.
            // It only resolves `var go = Instantiate(prefab); go.GetComponent<T>()`.
            if (hops < 3 && expression is IdentifierNameSyntax identifier &&
                model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol local)
            {
                SyntaxNode? body = EnclosingBody(identifier);
                if (body == null) return Origin.Other;
                if (IsReassigned(body, local, model, cancellationToken)) return Origin.Other;
                var declarator = local.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax(cancellationToken))
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault();
                if (declarator?.Initializer?.Value is ExpressionSyntax initializer)
                    return ClassifyExpression(initializer, model, cancellationToken, hops + 1);
            }

            return Origin.Other;
        }

        private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        continue;
                    case CastExpressionSyntax cast:
                        expression = cast.Expression;
                        continue;
                    case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression):
                        expression = binary.Left;
                        continue;
                    default:
                        return expression;
                }
            }
        }

        private static bool IsReassigned(SyntaxNode body, ILocalSymbol local, SemanticModel model,
            System.Threading.CancellationToken cancellationToken) =>
            body.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment =>
                assignment.Left is IdentifierNameSyntax name &&
                SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, local));

        private static bool IsGuaranteedByRequireComponent(SyntaxNode node, SemanticModel model,
            INamedTypeSymbol component, System.Threading.CancellationToken cancellationToken)
        {
            var declaration = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (declaration == null) return false;
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol owner) return false;

            for (INamedTypeSymbol? type = owner; type != null; type = type.BaseType)
            {
                foreach (AttributeData attribute in type.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name is not ("RequireComponent" or "RequireComponentAttribute")) continue;
                    foreach (TypedConstant argument in attribute.ConstructorArguments)
                    {
                        if (argument.Value is INamedTypeSymbol required && InheritsOrEquals(component, required))
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool InheritsOrEquals(INamedTypeSymbol type, INamedTypeSymbol candidate)
        {
            for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidate.OriginalDefinition))
                    return true;
            return false;
        }

        // ── dereference / verification analysis ──────────────────────────────────

        /// <summary>
        /// Returns the location of the first unverified dereference of the lookup result, or null
        /// when the result is verified, never dereferenced, or only reached through `?.`.
        /// </summary>
        private static Location? FindUnverifiedDereference(InvocationExpressionSyntax invocation,
            SyntaxNodeAnalysisContext context, out bool silentGuard, out Location? guardLocation)
        {
            silentGuard = false;
            guardLocation = null;

            SyntaxNode current = invocation;
            SyntaxNode? parent = invocation.Parent;

            // `lookup()?.Member` — the result itself is null-conditional.
            if (parent is ConditionalAccessExpressionSyntax conditional && conditional.Expression == current)
                return null;

            // `lookup().Member` / `lookup()[i]` — immediate unverified dereference.
            if (parent is MemberAccessExpressionSyntax member && member.Expression == current)
                return member.Name.GetLocation();
            if (parent is ElementAccessExpressionSyntax element && element.Expression == current)
                return element.GetLocation();

            // `var x = lookup();` — follow the local.
            if (parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } &&
                context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is ILocalSymbol local)
                return AnalyzeLocalUsage(declarator, local, context, out silentGuard, out guardLocation);

            // Compared to null, returned, passed as an argument, assigned to a field: not a dereference.
            return null;
        }

        private static Location? AnalyzeLocalUsage(VariableDeclaratorSyntax declarator, ILocalSymbol local,
            SyntaxNodeAnalysisContext context, out bool silentGuard, out Location? guardLocation)
        {
            silentGuard = false;
            guardLocation = null;

            SyntaxNode? body = EnclosingBody(declarator);
            if (body == null) return null;

            int declarationEnd = declarator.Span.End;
            var references = body.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(name => name.SpanStart >= declarationEnd)
                .Where(name => SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol, local))
                .OrderBy(name => name.SpanStart)
                .ToArray();

            foreach (IdentifierNameSyntax reference in references)
            {
                if (IsVerification(reference, out IfStatementSyntax? guard))
                {
                    if (guard != null && IsSilentGuard(guard))
                    {
                        silentGuard = true;
                        guardLocation = guard.Condition.GetLocation();
                    }
                    return null; // verified before any dereference
                }
                Location? dereference = DereferenceLocation(reference);
                if (dereference != null) return dereference;
            }
            return null;
        }

        private static bool IsVerification(IdentifierNameSyntax reference, out IfStatementSyntax? guard)
        {
            guard = null;
            SyntaxNode? parent = reference.Parent;

            // `x?.Member`
            if (parent is ConditionalAccessExpressionSyntax conditional && conditional.Expression == reference)
                return true;

            // `x ?? fallback`, `x ??= fallback`
            if (parent is BinaryExpressionSyntax coalesce && coalesce.IsKind(SyntaxKind.CoalesceExpression))
                return true;
            if (parent is AssignmentExpressionSyntax coalesceAssign &&
                coalesceAssign.IsKind(SyntaxKind.CoalesceAssignmentExpression))
                return true;

            // `x == null` / `x != null`
            if (parent is BinaryExpressionSyntax comparison &&
                (comparison.IsKind(SyntaxKind.EqualsExpression) || comparison.IsKind(SyntaxKind.NotEqualsExpression)))
            {
                guard = comparison.FirstAncestorOrSelf<IfStatementSyntax>();
                return true;
            }

            // `x is null`, `x is not null`, `x is T typed`
            if (parent is IsPatternExpressionSyntax pattern && pattern.Expression == reference)
            {
                guard = pattern.FirstAncestorOrSelf<IfStatementSyntax>();
                return true;
            }

            // `Object.ReferenceEquals(x, null)` and friends read the value without dereferencing it.
            if (parent is ArgumentSyntax) return false;

            return false;
        }

        /// <summary>A guard that leaves the method without throwing turns a crash into a silent no-op.</summary>
        private static bool IsSilentGuard(IfStatementSyntax guard)
        {
            var branches = new List<SyntaxNode>();
            if (guard.Statement != null) branches.Add(guard.Statement);
            if (guard.Else?.Statement != null) branches.Add(guard.Else.Statement);
            if (branches.Count == 0) return false;

            bool throws = branches.Any(branch =>
                branch is ThrowStatementSyntax ||
                branch.DescendantNodesAndSelf().Any(node => node is ThrowStatementSyntax or ThrowExpressionSyntax));
            if (throws) return false;

            return branches.Any(branch =>
                branch is ReturnStatementSyntax or ContinueStatementSyntax or BreakStatementSyntax ||
                branch.DescendantNodesAndSelf().Any(node =>
                    node is ReturnStatementSyntax or ContinueStatementSyntax or BreakStatementSyntax));
        }

        private static Location? DereferenceLocation(IdentifierNameSyntax reference)
        {
            SyntaxNode? parent = reference.Parent;
            if (parent is MemberAccessExpressionSyntax member && member.Expression == reference)
                return member.Name.GetLocation();
            if (parent is ElementAccessExpressionSyntax element && element.Expression == reference)
                return element.GetLocation();
            return null;
        }

        private static SyntaxNode? EnclosingBody(SyntaxNode node)
        {
            for (SyntaxNode? current = node; current != null; current = current.Parent)
            {
                switch (current)
                {
                    case AnonymousFunctionExpressionSyntax lambda:
                        return (SyntaxNode?)lambda.Block ?? lambda.ExpressionBody;
                    case LocalFunctionStatementSyntax localFunction:
                        return (SyntaxNode?)localFunction.Body ?? localFunction.ExpressionBody;
                    case AccessorDeclarationSyntax accessor:
                        return (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;
                    case BaseMethodDeclarationSyntax method:
                        return (SyntaxNode?)method.Body ?? method.ExpressionBody;
                }
            }
            return null;
        }

        // ── suppression ──────────────────────────────────────────────────────────

        /// <summary>
        /// Narrow reasoned suppression. The marker is honoured on the lookup line, on the reported
        /// line, or in the leading trivia of either enclosing statement. A marker without a
        /// non-empty reason does not suppress, so a typo fails closed rather than silently opting out.
        /// </summary>
        private static bool IsSuppressed(SyntaxNode lookup, Location reported)
        {
            if (HasExemption(LineText(lookup.GetLocation()))) return true;
            if (HasExemption(LineText(reported))) return true;

            var statement = lookup.FirstAncestorOrSelf<StatementSyntax>();
            if (statement != null && HasExemption(statement.GetLeadingTrivia().ToFullString())) return true;

            return false;
        }

        private static bool HasExemption(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            Match match = ExemptionMarker.Match(text);
            return match.Success && !string.IsNullOrWhiteSpace(match.Groups["reason"].Value);
        }

        private static string? LineText(Location location)
        {
            SyntaxTree? tree = location.SourceTree;
            if (tree == null || !location.IsInSource) return null;
            var text = tree.GetText();
            int line = location.GetLineSpan().StartLinePosition.Line;
            if (line < 0 || line >= text.Lines.Count) return null;
            return text.Lines[line].ToString();
        }
    }
}
