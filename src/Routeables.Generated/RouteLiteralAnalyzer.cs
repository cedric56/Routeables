using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Routeables.Generated;

/// <summary>
/// ROUT501: <c>GoToAsync("/monkies")</c> where no route known to the build has that address. A typo in a route string fails only
/// when the line runs; this reports it at build time. The routes known are the <c>[Routeable]</c> paths of the project and of the
/// projects it references, the <c>route</c> given to <c>RegisterRoute</c>, and the strings given as <c>route:</c> or assigned to a
/// <c>Route</c> property (menu entries). Reported as information, and not at all when a route is registered from a string the build
/// cannot read, or when no route is known (a library that is called with the routes of another project).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RouteLiteralAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ROUT501";

    private static readonly DiagnosticDescriptor Unknown = new DiagnosticDescriptor(
        DiagnosticId,
        "Route matches no known route",
        "No route known to this project matches '{0}'. Check the spelling against the [Routeable] paths and RegisterRoute calls, or ignore this message if the route is registered elsewhere.",
        "Routeables",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Unknown);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var routeable = start.Compilation.GetTypeByMetadataName("Routeables.Abstraction.RouteableAttribute");
            var router = start.Compilation.GetTypeByMetadataName("Routeables.Abstraction.Routing.IRouter");
            if (routeable is null || router is null)
                return;

            var templates = new ConcurrentBag<string>();
            var calls = new ConcurrentBag<(string Route, Location Where)>();
            var unreadable = false;

            foreach (var template in AttributeTemplates(start.Compilation.GlobalNamespace, routeable))
                templates.Add(template);

            start.RegisterOperationAction(ctx =>
            {
                var invocation = (IInvocationOperation)ctx.Operation;
                var method = invocation.TargetMethod;

                if (method.Name == "GoToAsync")
                {
                    if (Argument(invocation, "template") is not null)
                        return;                                          // the route to go to is given by the template
                    if (Argument(invocation, "url") is { } url && url.Value.ConstantValue is { HasValue: true, Value: string text })
                        calls.Add((text, invocation.Syntax.GetLocation()));
                    return;
                }

                foreach (var argument in invocation.Arguments)
                {
                    if (argument.Parameter?.Name != "route")
                        continue;
                    if (argument.Value.ConstantValue is { HasValue: true, Value: string route })
                        templates.Add(route);
                    else if (method.Name == "RegisterRoute" && !argument.IsImplicit)
                        unreadable = true;                               // registered from a string only known at run time
                }
            }, OperationKind.Invocation);

            start.RegisterOperationAction(ctx =>
            {
                var creation = (IObjectCreationOperation)ctx.Operation;
                foreach (var argument in creation.Arguments)
                    if (argument.Parameter?.Name == "route" && argument.Value.ConstantValue is { HasValue: true, Value: string route })
                        templates.Add(route);
            }, OperationKind.ObjectCreation);

            start.RegisterOperationAction(ctx =>
            {
                var assignment = (ISimpleAssignmentOperation)ctx.Operation;
                if (assignment.Target is IPropertyReferenceOperation { Property.Name: "Route" }
                    && assignment.Value.ConstantValue is { HasValue: true, Value: string route })
                    templates.Add(route);
            }, OperationKind.SimpleAssignment);

            // public string Route => "/about";   public string Route { get; } = "/about";
            start.RegisterSyntaxNodeAction(ctx =>
            {
                var property = (PropertyDeclarationSyntax)ctx.Node;
                if (property.Identifier.ValueText != "Route")
                    return;
                var value = property.ExpressionBody?.Expression ?? property.Initializer?.Value;
                if (value is not null && ctx.SemanticModel.GetConstantValue(value).Value is string route)
                    templates.Add(route);
            }, SyntaxKind.PropertyDeclaration);

            start.RegisterCompilationEndAction(ctx =>
            {
                if (unreadable || templates.IsEmpty)
                    return;

                var known = templates.ToArray();
                foreach (var (route, where) in calls)
                {
                    if (!IsChecked(route, out var address) || known.Any(t => Matches(t, address)))
                        continue;
                    ctx.ReportDiagnostic(Diagnostic.Create(Unknown, where, route));
                }
            });
        });
    }

    private static IArgumentOperation? Argument(IInvocationOperation invocation, string name)
        => invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == name && !a.IsImplicit);

    private static IEnumerable<string> AttributeTemplates(INamespaceSymbol ns, INamedTypeSymbol attribute)
    {
        foreach (var type in ns.GetTypeMembers())
            foreach (var found in FromType(type, attribute))
                yield return found;
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var found in AttributeTemplates(child, attribute))
                yield return found;
    }

    private static IEnumerable<string> FromType(INamedTypeSymbol type, INamedTypeSymbol attribute)
    {
        foreach (var data in type.GetAttributes())
            if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)
                && data.ConstructorArguments.Length > 0 && data.ConstructorArguments[0].Value is string path)
                yield return path;
        foreach (var nested in type.GetTypeMembers())
            foreach (var found in FromType(nested, attribute))
                yield return found;
    }

    // "-" and "-/orders" are qualifiers; a web address or a deep link is not a route of this application.
    private static bool IsChecked(string route, out string address)
    {
        address = route.Trim();
        if (address == "-") return false;
        if (address.StartsWith("-/", StringComparison.Ordinal)) address = address.Substring(1);
        if (address.Contains("://")) return false;
        address = Normal(address);
        return address.Length > 0;
    }

    private static string Normal(string route)
    {
        // The query starts at a '?' that is not the end of an optional placeholder: "{tab?}" and "{tab::?}" keep theirs.
        for (var i = 0; i < route.Length; i++)
        {
            if (route[i] == '?' && (i + 1 >= route.Length || route[i + 1] != '}'))
            {
                route = route.Substring(0, i);
                break;
            }
        }
        return route.Trim('/');
    }

    /// <summary>Whether <paramref name="address"/> (already normalised) fits <paramref name="template"/>: literals compared without case, a placeholder takes one segment, <c>{**x}</c> the rest, <c>{x?}</c> may be missing.</summary>
    internal static bool Matches(string template, string address)
    {
        var t = Normal(template).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var a = address.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var i = 0;
        for (var s = 0; s < t.Length; s++)
        {
            var segment = t[s];
            var placeholder = segment.Length > 1 && segment[0] == '{' && segment[segment.Length - 1] == '}';
            if (placeholder && segment.StartsWith("{**", StringComparison.Ordinal))
                return true;
            if (i >= a.Length)
                return placeholder && segment.EndsWith("?}", StringComparison.Ordinal) && t.Skip(s).All(x => x.EndsWith("?}", StringComparison.Ordinal));
            if (!placeholder && !string.Equals(segment, a[i], StringComparison.OrdinalIgnoreCase))
                return false;
            i++;
        }
        return i == a.Length;
    }
}
