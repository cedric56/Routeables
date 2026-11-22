using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Routeables.Generated
{
    // Reading the attribute and the constructor of the destination: which callers, which route, which parameters.
    public sealed partial class RouteCoordinationGenerator
    {

        private static AnalysisResult Analyze(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken ct)
        {
            var methods = new List<NavMethod>();
            var diagnostics = new List<DiagInfo>();

            var compilation = ctx.SemanticModel.Compilation;
            var routerType = compilation.GetTypeByMetadataName(RouterMetadataName);
            if (!(ctx.TargetSymbol is INamedTypeSymbol destination) || routerType is null)
                return AnalysisResult.Empty;

            var routeableAttribute = compilation.GetTypeByMetadataName(RouteableAttributeMetadataName);
            var context = new AnalysisContext(compilation, routerType);

            foreach (var attribute in ctx.Attributes)
            {
                ct.ThrowIfCancellationRequested();
                var location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation());

                void Report(string id, string message) => diagnostics.Add(new DiagInfo(id, message, location));

                AnalyzeAttribute(context, destination, routeableAttribute, attribute, location, Report, methods);
            }

            return new AnalysisResult(
                new EquatableArray<NavMethod>(methods.ToArray()),
                new EquatableArray<DiagInfo>(diagnostics.ToArray()));
        }

        private static void AnalyzeAttribute(
            AnalysisContext context,
            INamedTypeSymbol destination,
            INamedTypeSymbol? routeableAttribute,
            AttributeData attribute,
            LocationInfo? location,
            Action<string, string> report,
            List<NavMethod> methods)
        {
            var compilation = context.Compilation;

            if (!ValidateDestination(compilation, destination, out var destinationError))
            {
                report("ROUT102", destinationError!);
                return;
            }

            var callers = ReadCallers(compilation, destination, attribute, report);
            if (callers is null)
                return;

            if (!TryReadRoute(destination, routeableAttribute, attribute, report, out var template, out var explicitRoute, out var explicitMethodName))
                return;

            // The route string the method builds comes from Route when it is set; Template is only what is passed to the router to match it.
            // Without Route, both are the same text.
            var buildFrom = string.IsNullOrWhiteSpace(explicitRoute) ? template : explicitRoute!;
            if (!TryParseTemplate(buildFrom, out var parts, out var templateError))
            {
                report("ROUT104", "Route '" + buildFrom + "': " + templateError);
                return;
            }

            if (!TrySelectConstructor(context, destination, out var constructor, out var constructorError))
            {
                report("ROUT102", constructorError!);
                return;
            }

            var parameters = ClassifyParameters(context, constructor!, parts, buildFrom, report);
            if (parameters is null)
                return;

            WarnAboutAmbiguousMatching(context, destination, parameters, report);

            var (navParameters, entries) = SplitParameters(parameters);
            var routeOps = BuildRouteOps(parts, parameters);
            var methodName = explicitMethodName ?? DefaultMethodName(destination);
            var destinationName = destination.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            foreach (var caller in callers)
            {
                var ns = caller.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : caller.ContainingNamespace.ToDisplayString();
                var hint = CallerHint(caller);

                methods.Add(new NavMethod(
                    CallerName: caller.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    CallerNamespace: ns,
                    CallerHint: hint,
                    MethodName: methodName,
                    DestinationName: destinationName,
                    DestinationDisplay: destination.ToDisplayString(),
                    Template: template,
                    LeadingSlash: buildFrom.StartsWith("/", StringComparison.Ordinal),
                    Parameters: new EquatableArray<NavParam>(navParameters.ToArray()),
                    Entries: new EquatableArray<EntryOp>(entries.ToArray()),
                    RouteOps: new EquatableArray<RouteOp>(routeOps.ToArray()),
                    Location: location));
            }
        }

        // The caller types of [RouteableViewModel], or null when there is nothing to build for (the problem is reported, unless the compiler already does).
        private static List<INamedTypeSymbol>? ReadCallers(
            Compilation compilation, INamedTypeSymbol destination, AttributeData attribute, Action<string, string> report)
        {
            var callers = new List<INamedTypeSymbol>();
            var callersValid = true;
            foreach (var typed in EnumerateCallerArguments(attribute))
            {
                if (typed.Value is ITypeSymbol unresolved && unresolved.TypeKind == TypeKind.Error)
                    return null; // the compiler already reports the unresolved type

                if (!(typed.Value is INamedTypeSymbol caller))
                {
                    var name = typed.Value is ITypeSymbol other ? other.ToDisplayString() : "<null>";
                    report("ROUT101", "'" + name + "' cannot be a caller: it must be a class, struct or interface.");
                    callersValid = false;
                    continue;
                }

                var callerError = ValidateCallerError(compilation, caller);
                if (callerError is not null)
                {
                    report("ROUT101", "'" + caller.Name + "' cannot be a caller: " + callerError);
                    callersValid = false;
                    continue;
                }

                callers.Add(caller);
            }

            if (callers.Count == 0 && callersValid)
            {
                report("ROUT101", "'" + destination.Name + "': [RouteableViewModel] needs at least one caller type, for example [RouteableViewModel(typeof(HomeViewModel))].");
                return null;
            }

            return callersValid ? callers : null;
        }

        // The template the router matches, the Route the method builds from (when set) and the method name (when set).
        // False when the route cannot be known; the problem is reported.
        private static bool TryReadRoute(
            INamedTypeSymbol destination,
            INamedTypeSymbol? routeableAttribute,
            AttributeData attribute,
            Action<string, string> report,
            out string template,
            out string? explicitRoute,
            out string? explicitMethodName)
        {
            template = string.Empty;
            explicitRoute = null;
            explicitMethodName = null;
            string? explicitTemplate = null;
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "Route")
                    explicitRoute = named.Value.Value as string;
                else if (named.Key == "Template")
                    explicitTemplate = named.Value.Value as string;
                else if (named.Key == "MethodName")
                    explicitMethodName = named.Value.Value as string;
            }

            if (explicitMethodName is not null && !SyntaxFacts.IsValidIdentifier(explicitMethodName))
            {
                report("ROUT102", "'" + explicitMethodName + "' is not a valid method name.");
                return false;
            }

            // Template falls back to Route, which falls back to the path of the single [Routeable].
            var chosen = string.IsNullOrWhiteSpace(explicitTemplate) ? explicitRoute : explicitTemplate;
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                template = chosen!;
                return true;
            }

            var paths = new List<string>();
            foreach (var data in destination.GetAttributes())
            {
                if (routeableAttribute is not null &&
                    SymbolEqualityComparer.Default.Equals(data.AttributeClass, routeableAttribute) &&
                    data.ConstructorArguments.Length > 0 &&
                    data.ConstructorArguments[0].Value is string path)
                    paths.Add(path);
            }

            if (paths.Count == 1)
            {
                template = paths[0];
                return true;
            }

            report("ROUT103", paths.Count == 0
                ? "'" + destination.Name + "' has no route: add [Routeable(\"/path\")] or set Route on [RouteableViewModel]."
                : "'" + destination.Name + "' has several [Routeable] routes: set Route on [RouteableViewModel] to choose one.");
            return false;
        }

        // What the generated method takes, and what it hands to the navigation entry point.
        private static (List<NavParam> Parameters, List<EntryOp> Entries) SplitParameters(IEnumerable<ParamInfo> parameters)
        {
            var navParameters = new List<NavParam>();
            var entries = new List<EntryOp>();
            foreach (var p in parameters)
            {
                switch (p.Kind)
                {
                    case ParamKind.Router:
                        entries.Add(new EntryOp(true, string.Empty, false));
                        break;
                    case ParamKind.Container:
                        break;
                    default:
                        navParameters.Add(p.ToNavParam());
                        if (p.Kind == ParamKind.Entry)
                            entries.Add(new EntryOp(false, p.Id, p.NeedsConditionalEntry));
                        break;
                }
            }
            return (navParameters, entries);
        }

        private static IEnumerable<TypedConstant> EnumerateCallerArguments(AttributeData attribute)
        {
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Kind == TypedConstantKind.Array)
                {
                    if (argument.IsNull)
                        continue;

                    foreach (var value in argument.Values)
                        yield return value;
                }
                else
                {
                    yield return argument;
                }
            }
        }


        private static bool ValidateDestination(Compilation compilation, INamedTypeSymbol destination, out string? error)
        {
            error = null;
            if (destination.TypeKind != TypeKind.Class || destination.IsStatic || destination.IsAbstract)
                error = "'" + destination.Name + "' must be a concrete, non-static class.";
            else if (HasGenericScope(destination))
                error = "'" + destination.Name + "' cannot be generic (or nested in a generic type).";
            else if (!compilation.IsSymbolAccessibleWithin(destination, compilation.Assembly))
                error = "'" + destination.Name + "' must be public or internal so the generated code can reference it.";

            return error is null;
        }

        private static string? ValidateCallerError(Compilation compilation, INamedTypeSymbol caller)
        {
            if (caller.TypeKind != TypeKind.Class && caller.TypeKind != TypeKind.Struct && caller.TypeKind != TypeKind.Interface)
                return "it must be a class, struct or interface.";
            if (caller.IsStatic)
                return "a static class cannot be the target of an extension method.";
            if (HasGenericScope(caller))
                return "generic callers are not supported.";
            if (!compilation.IsSymbolAccessibleWithin(caller, compilation.Assembly))
                return "it is not accessible from this assembly.";
            return null;
        }

        private static bool HasGenericScope(INamedTypeSymbol type)
        {
            for (var t = type; t is not null; t = t.ContainingType)
            {
                if (t.IsGenericType)
                    return true;
            }

            return false;
        }
    }
}
