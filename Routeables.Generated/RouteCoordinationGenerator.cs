using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Routeables.Generated
{
    /// <summary>
    /// Generates strongly typed routing methods for view models decorated with
    /// <c>[RouteableViewModel(typeof(Caller))]</c>: one extension method per declared caller type,
    /// whose parameters mirror the destination's constructor.
    /// </summary>
    [Generator]
    public sealed class RouteCoordinationGenerator : IIncrementalGenerator
    {
        private const string ViewModelAttributeMetadataName = "Routeables.Abstraction.RouteableViewModelAttribute";
        private const string RouteableAttributeMetadataName = "Routeables.Abstraction.RouteableAttribute";
        private const string RouterMetadataName = "Routeables.Abstraction.IRouter";
        private const string FromContainerMetadataName = "Routeables.Abstraction.FromContainerAttribute";
        private const string FromKeyedServicesMetadataName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";
        private const string FromServicesMetadataName = "Microsoft.AspNetCore.Mvc.FromServicesAttribute";
        private const string ActivatorCtorMetadataName = "Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructorAttribute";

        private const string Category = "Routeables.Routing";

        private static readonly Dictionary<string, DiagnosticDescriptor> Descriptors = new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["ROUT101"] = Descriptor("ROUT101", "Invalid route caller", DiagnosticSeverity.Error),
            ["ROUT102"] = Descriptor("ROUT102", "Invalid route destination", DiagnosticSeverity.Error),
            ["ROUT103"] = Descriptor("ROUT103", "Route not found", DiagnosticSeverity.Error),
            ["ROUT104"] = Descriptor("ROUT104", "Invalid route template", DiagnosticSeverity.Error),
            ["ROUT105"] = Descriptor("ROUT105", "Unsupported constructor parameter", DiagnosticSeverity.Error),
            ["ROUT106"] = Descriptor("ROUT106", "Ambiguous constructor argument matching", DiagnosticSeverity.Warning),
            ["ROUT107"] = Descriptor("ROUT107", "Duplicate route method", DiagnosticSeverity.Error),
        };

        private static DiagnosticDescriptor Descriptor(string id, string title, DiagnosticSeverity severity) =>
            new DiagnosticDescriptor(id, title, "{0}", Category, severity, isEnabledByDefault: true);

        // Mirrors RouteParser.Param: {name}, {name:type}, {name:type?}, {name::type} (view-bound).
        private static readonly Regex Placeholder =
            new Regex(@"^\{(?<name>\w+)(?::(?<attribute>:)?(?<type>[^}?]*))?(?<opt>\?)?\}$", RegexOptions.CultureInvariant);

        private static readonly string[] ReservedNames = { "caller", "router", "cancellationToken", "__args", "__route", "__separator" };

        // Type of the generated method parameter, with the default value when there is one.
        private static readonly SymbolDisplayFormat ParameterFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeDefaultValue);

        private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var results = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ViewModelAttributeMetadataName,
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, ct) => Analyze(ctx, ct))
                .Collect();

            context.RegisterSourceOutput(results, static (spc, all) => Emit(spc, all));
        }

        // ------------------------------------------------------------------ analysis

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

            // ---- destination
            if (!ValidateDestination(compilation, destination, out var destinationError))
            {
                report("ROUT102", destinationError!);
                return;
            }

            // ---- callers
            var callers = ReadCallers(compilation, destination, attribute, report);
            if (callers is null)
                return;

            // ---- route template
            if (!TryReadRoute(destination, routeableAttribute, attribute, report, out var template, out var explicitRoute, out var explicitMethodName))
                return;

            // ---- template
            // The route string the method builds comes from Route when it is set; Template is only what is
            // passed to the router to match it. Without Route, both are the same text.
            var buildFrom = string.IsNullOrWhiteSpace(explicitRoute) ? template : explicitRoute!;
            if (!TryParseTemplate(buildFrom, out var parts, out var templateError))
            {
                report("ROUT104", "Route '" + buildFrom + "': " + templateError);
                return;
            }

            // ---- constructor
            if (!TrySelectConstructor(context, destination, out var constructor, out var constructorError))
            {
                report("ROUT102", constructorError!);
                return;
            }

            var parameters = ClassifyParameters(context, constructor!, parts, buildFrom, report);
            if (parameters is null)
                return;

            WarnAboutAmbiguousMatching(context, destination, parameters, report);

            // ---- model
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

        // The caller types of [RouteableViewModel], or null when there is none to build for (the problem is reported, unless the
        // compiler reports it already).
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

        // The template the router matches, the Route the method builds from (when it is set) and the method name (when it is set).
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

        // What the generated method takes, and what it hands to the route change entry.
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

        // ---- destination / caller validation

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

        // ---- template

        private static bool TryParseTemplate(string template, out List<TemplatePart> parts, out string error)
        {
            parts = new List<TemplatePart>();
            error = string.Empty;

            SplitTemplate(template, out var path, out var query);

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!TryReadPart(segments[i], isQuery: false, out var part, out error))
                    return false;
                parts.Add(part);
            }

            var queryParts = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < queryParts.Length; i++)
            {
                if (!TryReadPart(queryParts[i], isQuery: true, out var part, out error))
                    return false;
                parts.Add(part);
            }

            // An omitted optional path segment can only be a trailing one.
            var lastPath = -1;
            for (var i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsQuery)
                    lastPath = i;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (!part.IsPlaceholder)
                    continue;

                if (part.IsViewBound)
                {
                    error = "the view-bound placeholder {" + part.Name + "::" + part.Type + "} is not supported by generated route methods.";
                    return false;
                }

                if (!names.Add(part.Name))
                {
                    error = "the placeholder {" + part.Name + "} is used twice.";
                    return false;
                }

                if (part.Optional && !part.IsQuery && i != lastPath)
                {
                    error = "the optional placeholder {" + part.Name + "?} must be the last path segment.";
                    return false;
                }

                if (ClrKindOf(part.Type) == ClrKind.None)
                {
                    error = "the placeholder {" + part.Name + ":" + part.Type + "} has a type that generated methods cannot format (supported: string, int, long, bool, guid, decimal, double).";
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadPart(string text, bool isQuery, out TemplatePart part, out string error)
        {
            error = string.Empty;
            var match = Placeholder.Match(text);
            if (match.Success)
            {
                part = new TemplatePart(
                    isQuery,
                    isPlaceholder: true,
                    text,
                    match.Groups["name"].Value,
                    match.Groups["type"].Value,
                    match.Groups["attribute"].Success,
                    match.Groups["opt"].Success);
                return true;
            }

            if (text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0)
            {
                part = TemplatePart.Literal(isQuery, text);
                error = "'" + text + "' is not a valid placeholder (expected {name}, {name:type} or {name:type?}).";
                return false;
            }

            part = TemplatePart.Literal(isQuery, text);
            return true;
        }

        // Same rule as RouteParser.Split: a '?' inside braces does not start the query.
        private static void SplitTemplate(string template, out string path, out string query)
        {
            var depth = 0;
            for (var i = 0; i < template.Length; i++)
            {
                var c = template[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;
                else if (c == '?' && depth == 0)
                {
                    path = template.Substring(0, i);
                    query = template.Substring(i + 1);
                    return;
                }
            }

            path = template;
            query = string.Empty;
        }

        private static ClrKind ClrKindOf(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "":
                case "string":
                    return ClrKind.String;
                case "int":
                    return ClrKind.Int;
                case "long":
                    return ClrKind.Long;
                case "bool":
                    return ClrKind.Bool;
                case "guid":
                    return ClrKind.Guid;
                case "decimal":
                    return ClrKind.Decimal;
                case "double":
                    return ClrKind.Double;
                default:
                    return ClrKind.None;
            }
        }

        private static ClrKind ClrKindOf(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                type = named.TypeArguments[0];

            switch (type.SpecialType)
            {
                case SpecialType.System_String: return ClrKind.String;
                case SpecialType.System_Int32: return ClrKind.Int;
                case SpecialType.System_Int64: return ClrKind.Long;
                case SpecialType.System_Boolean: return ClrKind.Bool;
                case SpecialType.System_Decimal: return ClrKind.Decimal;
                case SpecialType.System_Double: return ClrKind.Double;
            }

            return type.ToDisplayString() == "System.Guid" ? ClrKind.Guid : ClrKind.None;
        }

        // ---- constructor

        private static bool TrySelectConstructor(
            AnalysisContext context,
            INamedTypeSymbol destination,
            out IMethodSymbol? constructor,
            out string? error)
        {
            constructor = null;
            error = null;

            var candidates = new List<IMethodSymbol>();
            foreach (var c in destination.InstanceConstructors)
            {
                if (c.DeclaredAccessibility == Accessibility.Public)
                    candidates.Add(c);
            }

            if (candidates.Count == 0)
            {
                error = "'" + destination.Name + "' has no public constructor.";
                return false;
            }

            var marked = new List<IMethodSymbol>();
            foreach (var c in candidates)
            {
                foreach (var a in c.GetAttributes())
                {
                    if (a.AttributeClass?.ToDisplayString() == ActivatorCtorMetadataName)
                    {
                        marked.Add(c);
                        break;
                    }
                }
            }

            if (marked.Count > 1)
            {
                error = "'" + destination.Name + "' has several constructors marked [ActivatorUtilitiesConstructor].";
                return false;
            }

            if (marked.Count == 1)
            {
                constructor = marked[0];
                return true;
            }

            var max = 0;
            foreach (var c in candidates)
                max = Math.Max(max, c.Parameters.Length);

            var longest = candidates.Where(c => c.Parameters.Length == max).ToList();
            if (longest.Count > 1)
            {
                error = "'" + destination.Name + "' has several public constructors with " + max + " parameters: mark one with [ActivatorUtilitiesConstructor].";
                return false;
            }

            constructor = longest[0];
            return true;
        }

        private static List<ParamInfo>? ClassifyParameters(
            AnalysisContext context,
            IMethodSymbol constructor,
            List<TemplatePart> parts,
            string template,
            Action<string, string> report)
        {
            var compilation = context.Compilation;
            var result = new List<ParamInfo>();
            var failed = false;

            for (var i = 0; i < constructor.Parameters.Length; i++)
            {
                var p = constructor.Parameters[i];

                if (p.RefKind != RefKind.None || p.Type.TypeKind == TypeKind.Pointer ||
                    p.Type.TypeKind == TypeKind.FunctionPointer || p.Type.IsRefLikeType)
                {
                    report("ROUT105", "Constructor parameter '" + p.Name + "' (" + p.Type.ToDisplayString() + ") cannot be passed through a route method: ref, out, in, pointer and ref struct parameters are not supported.");
                    failed = true;
                    continue;
                }

                var kind = ParamKind.Entry;
                if (SymbolEqualityComparer.Default.Equals(p.Type, context.RouterType))
                    kind = ParamKind.Router;
                else if (IsResolvedFromContainer(p))
                    kind = ParamKind.Container;

                result.Add(new ParamInfo(p, i, kind, compilation));
            }

            if (failed)
                return null;

            // Route placeholders -> constructor parameters (by name, like the route parser fills them).
            foreach (var part in parts)
            {
                if (!part.IsPlaceholder)
                    continue;

                ParamInfo? target = null;
                foreach (var info in result)
                {
                    if (info.Kind == ParamKind.Entry && string.Equals(info.Symbol.Name, part.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        target = info;
                        break;
                    }
                }

                if (target is null)
                {
                    report("ROUT104", "Route '" + template + "': the placeholder {" + part.Name + "} has no matching constructor parameter. " +
                        "Parameters of type IRouter or marked [FromContainer] cannot receive route values.");
                    failed = true;
                    continue;
                }

                var expected = ClrKindOf(part.Type);
                var actual = ClrKindOf(target.Symbol.Type);
                if (actual != expected)
                {
                    report("ROUT104", "Route '" + template + "': the placeholder {" + part.Name + ":" + part.Type + "} does not match the type " +
                        target.Symbol.Type.ToDisplayString() + " of constructor parameter '" + target.Symbol.Name + "'.");
                    failed = true;
                    continue;
                }

                if (part.Optional && !target.HasDefault)
                {
                    report("ROUT104", "Route '" + template + "': the placeholder {" + part.Name + "?} is optional, so constructor parameter '" +
                        target.Symbol.Name + "' needs a default value.");
                    failed = true;
                    continue;
                }

                target.BecomeRoute(part.Optional, expected);
            }

            if (failed)
                return null;

            foreach (var info in result)
            {
                if (info.Kind != ParamKind.Entry && info.Kind != ParamKind.Route)
                    continue;

                foreach (var reserved in ReservedNames)
                {
                    if (string.Equals(info.Symbol.Name, reserved, StringComparison.Ordinal))
                    {
                        report("ROUT105", "Constructor parameter '" + info.Symbol.Name + "' clashes with a name used by the generated method: rename it.");
                        failed = true;
                    }
                }
            }

            return failed ? null : result;
        }

        private static bool IsResolvedFromContainer(IParameterSymbol parameter)
        {
            foreach (var a in parameter.GetAttributes())
            {
                var name = a.AttributeClass?.ToDisplayString();
                if (name == FromContainerMetadataName || name == FromKeyedServicesMetadataName || name == FromServicesMetadataName)
                    return true;
            }

            return false;
        }

        // ActivatorUtilities matches the given arguments to constructor parameters by type, in order.
        // A parameter that may not receive an entry of its own (optional, from the container, or filled
        // from the route afterwards) can swallow the value meant for another parameter of a compatible type.
        private static void WarnAboutAmbiguousMatching(
            AnalysisContext context,
            INamedTypeSymbol destination,
            List<ParamInfo> parameters,
            Action<string, string> report)
        {
            foreach (var gap in parameters)
            {
                var mayBeUnfilled = gap.Kind == ParamKind.Container || gap.Kind == ParamKind.Route ||
                                    (gap.Kind == ParamKind.Entry && gap.HasDefault);
                if (!mayBeUnfilled)
                    continue;

                foreach (var other in parameters)
                {
                    var supplies = other.Kind == ParamKind.Router || other.Kind == ParamKind.Entry;
                    if (!supplies || ReferenceEquals(other, gap))
                        continue;

                    // Entries before the gap are already matched to their own parameters.
                    if (gap.Kind != ParamKind.Route && other.Index < gap.Index)
                        continue;

                    if (Assignable(context.Compilation, other.Symbol.Type, gap.Symbol.Type) ||
                        Assignable(context.Compilation, gap.Symbol.Type, other.Symbol.Type))
                    {
                        report("ROUT106", "'" + destination.Name + "': constructor parameter '" + gap.Symbol.Name + "' (" + gap.Symbol.Type.ToDisplayString() +
                            ") may receive the value meant for '" + other.Symbol.Name + "' (" + other.Symbol.Type.ToDisplayString() +
                            ") because the container matches constructor arguments by type. Use distinct types.");
                        break;
                    }
                }
            }
        }

        private static bool Assignable(Compilation compilation, ITypeSymbol from, ITypeSymbol to)
        {
            var c = compilation.ClassifyConversion(from, to);
            return c.Exists && c.IsImplicit && (c.IsIdentity || c.IsReference || c.IsBoxing || c.IsNullable);
        }

        // ---- route operations

        private static List<RouteOp> BuildRouteOps(List<TemplatePart> parts, List<ParamInfo> parameters)
        {
            var ops = new List<RouteOp>();
            var firstPath = true;

            foreach (var part in parts)
            {
                // Path segments are joined with '/'; the template's own leading slash is written separately.
                var prefix = string.Empty;
                if (!part.IsQuery)
                {
                    prefix = firstPath ? string.Empty : "/";
                    firstPath = false;
                }

                if (!part.IsPlaceholder)
                {
                    ops.Add(new RouteOp(
                        part.IsQuery ? RouteOpKind.QueryLiteral : RouteOpKind.PathLiteral,
                        prefix, part.Text, string.Empty, ClrKind.None,
                        Optional: false, Conditional: false, NullableValue: false));
                    continue;
                }

                ParamInfo? info = null;
                foreach (var candidate in parameters)
                {
                    if (candidate.Kind == ParamKind.Route && string.Equals(candidate.Symbol.Name, part.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        info = candidate;
                        break;
                    }
                }

                ops.Add(new RouteOp(
                    part.IsQuery ? RouteOpKind.QueryParam : RouteOpKind.PathParam,
                    prefix,
                    part.IsQuery ? part.Name : string.Empty,
                    info!.Id,
                    info.RouteKind,
                    Optional: part.Optional,
                    Conditional: part.Optional && info.CanBeNull,
                    NullableValue: info.KeepsNullableValue));
            }

            return ops;
        }

        private static string DefaultMethodName(INamedTypeSymbol destination)
        {
            var name = destination.Name;
            const string suffix = "ViewModel";
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - suffix.Length);

            return "GoTo" + name + "Async";
        }

        private static string CallerHint(INamedTypeSymbol caller)
        {
            var sb = new StringBuilder();
            var chain = new List<string>();
            for (var t = caller; t is not null; t = t.ContainingType)
                chain.Insert(0, t.Name);

            if (!caller.ContainingNamespace.IsGlobalNamespace)
                sb.Append(caller.ContainingNamespace.ToDisplayString().Replace('.', '_')).Append('_');

            sb.Append(string.Join("_", chain));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ emission

        private static void Emit(SourceProductionContext spc, ImmutableArray<AnalysisResult> results)
        {
            var methods = new List<NavMethod>();
            foreach (var result in results)
            {
                foreach (var d in result.Diagnostics)
                    spc.ReportDiagnostic(Diagnostic.Create(Descriptors[d.Id], d.Location?.ToLocation() ?? Location.None, d.Message));

                methods.AddRange(result.Methods);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var accepted = new List<NavMethod>();
            foreach (var m in methods
                .OrderBy(m => m.CallerName, StringComparer.Ordinal)
                .ThenBy(m => m.MethodName, StringComparer.Ordinal)
                .ThenBy(m => m.DestinationName, StringComparer.Ordinal))
            {
                var signature = m.CallerName + "|" + m.MethodName + "|" + string.Join(",", m.Parameters.Select(p => p.TypeDisplay));
                if (!seen.Add(signature))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        Descriptors["ROUT107"],
                        m.Location?.ToLocation() ?? Location.None,
                        "The method " + m.MethodName + " is already generated for " + m.CallerName + " with the same parameter types: set MethodName on [RouteableViewModel] to tell them apart."));
                    continue;
                }

                accepted.Add(m);
            }

            foreach (var group in accepted.GroupBy(m => m.CallerName))
            {
                var first = group.First();
                spc.AddSource(
                    "RouteCoordination." + Sanitize(first.CallerHint) + ".g.cs",
                    SourceText.From(EmitCaller(first, group.ToList()), Encoding.UTF8));
            }
        }

        private static string EmitCaller(NavMethod first, List<NavMethod> methods)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            var indent = "";
            if (first.CallerNamespace.Length > 0)
            {
                sb.AppendLine("namespace " + first.CallerNamespace);
                sb.AppendLine("{");
                indent = "    ";
            }

            sb.AppendLine(indent + "internal static class " + first.CallerHint + "RouteCoordination");
            sb.AppendLine(indent + "{");

            for (var i = 0; i < methods.Count; i++)
            {
                if (i > 0)
                    sb.AppendLine();
                EmitMethod(sb, indent + "    ", methods[i]);
            }

            sb.AppendLine(indent + "}");

            if (first.CallerNamespace.Length > 0)
                sb.AppendLine("}");

            return sb.ToString();
        }

        private static void EmitMethod(StringBuilder sb, string indent, NavMethod m)
        {
            const string returnType = "global::System.Threading.Tasks.Task<global::Routeables.Abstraction.RouteableResult>";

            // Only a <summary>: a partial set of <param> tags would raise CS1573 in projects that treat warnings as errors.
            sb.AppendLine(indent + "/// <summary>Goes to <c>" + Xml(m.DestinationDisplay) + "</c> (route <c>" + Xml(m.Template) + "</c>).</summary>");
            sb.AppendLine(indent + "internal static " + returnType + " " + m.MethodName + "(");
            sb.AppendLine(indent + "    this " + m.CallerName + " caller,");
            sb.AppendLine(indent + "    global::Routeables.Abstraction.IRouter router,");
            foreach (var p in m.Parameters)
                sb.AppendLine(indent + "    " + p.Declaration + ",");
            sb.AppendLine(indent + "    global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine(indent + "{");

            var body = indent + "    ";
            sb.AppendLine(body + "global::System.ArgumentNullException.ThrowIfNull(router);");
            foreach (var p in m.Parameters)
            {
                if (p.CheckNotEmpty)
                    sb.AppendLine(body + "global::System.ArgumentException.ThrowIfNullOrEmpty(" + p.Id + ");");
                else if (p.CheckNotNull)
                    sb.AppendLine(body + "global::System.ArgumentNullException.ThrowIfNull(" + p.Id + ");");
            }

            EmitRoute(sb, body, m);

            sb.AppendLine(body + "var __args = global::Routeables.Routing.RouteableArgs.Create();");
            foreach (var e in m.Entries)
            {
                var value = e.IsRouter ? "router" : e.ParamId;
                var statement = "__args.ForViewModel(new global::Routeables.Abstraction.RouteableArgument(" + value + "));";
                sb.AppendLine(body + (e.Conditional ? "if (" + value + " is not null) " : string.Empty) + statement);
            }

            sb.AppendLine(body + "return router.GoToRouteAsync<" + m.DestinationName + ">(__route, __args, " + Literal(m.Template) + ", cancellationToken);");
            sb.AppendLine(indent + "}");
        }

        private static void EmitRoute(StringBuilder sb, string indent, NavMethod m)
        {
            var expressions = new List<string>();
            var pending = new StringBuilder();
            if (m.LeadingSlash)
                pending.Append('/');

            var deferred = new List<RouteOp>();
            var queryOps = new List<RouteOp>();

            void Flush()
            {
                if (pending.Length == 0)
                    return;
                expressions.Add(Literal(pending.ToString()));
                pending.Clear();
            }

            foreach (var op in m.RouteOps)
            {
                switch (op.Kind)
                {
                    case RouteOpKind.PathLiteral:
                        pending.Append(op.Prefix).Append(op.Text);
                        break;
                    case RouteOpKind.PathParam when op.Optional:
                        deferred.Add(op);
                        break;
                    case RouteOpKind.PathParam:
                        pending.Append(op.Prefix);
                        Flush();
                        expressions.Add(Format(op.Clr, ValueOf(op)));
                        break;
                    default:
                        queryOps.Add(op);
                        break;
                }
            }

            Flush();
            sb.AppendLine(indent + "var __route = " + (expressions.Count == 0 ? "\"\"" : string.Join(" + ", expressions)) + ";");

            foreach (var op in deferred)
            {
                var statement = "__route += " + Literal(op.Prefix) + " + " + Format(op.Clr, ValueOf(op)) + ";";
                sb.AppendLine(indent + (op.Conditional ? "if (" + op.ParamId + " is not null) " : string.Empty) + statement);
            }

            if (queryOps.Count == 0)
                return;

            sb.AppendLine(indent + "var __separator = '?';");
            foreach (var op in queryOps)
            {
                if (op.Kind == RouteOpKind.QueryLiteral)
                {
                    sb.AppendLine(indent + "__route += __separator + " + Literal(op.Text) + ";");
                    sb.AppendLine(indent + "__separator = '&';");
                    continue;
                }

                var add = "{ __route += __separator + " + Literal(op.Text + "=") + " + " + Format(op.Clr, ValueOf(op)) + "; __separator = '&'; }";
                sb.AppendLine(indent + (op.Conditional ? "if (" + op.ParamId + " is not null) " : string.Empty) + add);
            }
        }

        private static string ValueOf(RouteOp op) => op.NullableValue ? op.ParamId + ".Value" : op.ParamId;

        private static string Format(ClrKind kind, string value)
        {
            switch (kind)
            {
                case ClrKind.Int:
                case ClrKind.Long:
                case ClrKind.Decimal:
                case ClrKind.Double:
                    return "global::System.Uri.EscapeDataString(" + value + ".ToString(global::System.Globalization.CultureInfo.InvariantCulture))";
                case ClrKind.Bool:
                    return "(" + value + " ? \"true\" : \"false\")";
                case ClrKind.Guid:
                    return "global::System.Uri.EscapeDataString(" + value + ".ToString())";
                default:
                    return "global::System.Uri.EscapeDataString(" + value + ")";
            }
        }

        private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

        private static string Xml(string value) =>
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string Sanitize(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ models

        private sealed class AnalysisContext
        {
            public AnalysisContext(Compilation compilation, INamedTypeSymbol routerType)
            {
                Compilation = compilation;
                RouterType = routerType;
            }

            public Compilation Compilation { get; }
            public INamedTypeSymbol RouterType { get; }
        }

        private enum ParamKind { Entry, Route, Router, Container }

        // Working data for one constructor parameter; never stored in the incremental model.
        private sealed class ParamInfo
        {
            public ParamInfo(IParameterSymbol symbol, int index, ParamKind kind, Compilation compilation)
            {
                Symbol = symbol;
                Index = index;
                Kind = kind;
                Compilation = compilation;
            }

            public IParameterSymbol Symbol { get; }
            public int Index { get; }
            public ParamKind Kind { get; private set; }
            public Compilation Compilation { get; }
            public ClrKind RouteKind { get; private set; }
            public bool RouteOptional { get; private set; }

            public string Id => EscapeIdentifier(Symbol.Name);
            public bool HasDefault => Symbol.HasExplicitDefaultValue;
            private bool IsNullableValueType => Symbol.Type is INamedTypeSymbol n && n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

            // Null (or an unset optional value) can legitimately reach the generated method.
            public bool CanBeNull => Symbol.Type.IsReferenceType || IsNullableValueType;

            // An optional Nullable<T> keeps its type, so the value is read with .Value.
            public bool KeepsNullableValue => HasDefault && IsNullableValueType;

            // A null optional entry is simply not passed on.
            public bool NeedsConditionalEntry => HasDefault && CanBeNull;

            public void BecomeRoute(bool optional, ClrKind kind)
            {
                Kind = ParamKind.Route;
                RouteOptional = optional;
                RouteKind = kind;
            }

            public NavParam ToNavParam()
            {
                string declaration;
                string typeDisplay;
                if (HasDefault)
                {
                    declaration = Symbol.ToDisplayString(ParameterFormat);
                    typeDisplay = Symbol.Type.ToDisplayString(TypeFormat);
                }
                else
                {
                    // Without a default the value is mandatory: the container cannot be given a null.
                    var type = Symbol.Type;
                    if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                        type = named.TypeArguments[0];
                    else if (type.IsReferenceType)
                        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

                    typeDisplay = type.ToDisplayString(TypeFormat);
                    declaration = typeDisplay + " " + Id;
                }

                var isString = Symbol.Type.SpecialType == SpecialType.System_String;
                var checkNotEmpty = false;
                var checkNotNull = false;
                if (Kind == ParamKind.Route)
                {
                    if (!RouteOptional)
                    {
                        checkNotEmpty = isString;
                        checkNotNull = !isString && KeepsNullableValue;
                    }
                }
                else
                {
                    checkNotNull = !HasDefault && Symbol.Type.IsReferenceType;
                }

                return new NavParam(Id, declaration, typeDisplay, checkNotNull, checkNotEmpty);
            }
        }

        private static string EscapeIdentifier(string name) =>
            SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

        private sealed record AnalysisResult(EquatableArray<NavMethod> Methods, EquatableArray<DiagInfo> Diagnostics)
        {
            public static readonly AnalysisResult Empty = new AnalysisResult(
                new EquatableArray<NavMethod>(Array.Empty<NavMethod>()),
                new EquatableArray<DiagInfo>(Array.Empty<DiagInfo>()));
        }

        private sealed record DiagInfo(string Id, string Message, LocationInfo? Location);

        private sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
        {
            public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

            public static LocationInfo? From(Location? location)
            {
                if (location is null || location.SourceTree is null)
                    return null;

                return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
            }
        }

        private sealed record NavMethod(
            string CallerName,
            string CallerNamespace,
            string CallerHint,
            string MethodName,
            string DestinationName,
            string DestinationDisplay,
            string Template,
            bool LeadingSlash,
            EquatableArray<NavParam> Parameters,
            EquatableArray<EntryOp> Entries,
            EquatableArray<RouteOp> RouteOps,
            LocationInfo? Location);

        private sealed record NavParam(string Id, string Declaration, string TypeDisplay, bool CheckNotNull, bool CheckNotEmpty);

        private sealed record EntryOp(bool IsRouter, string ParamId, bool Conditional);

        private enum RouteOpKind { PathLiteral, PathParam, QueryLiteral, QueryParam }

        private enum ClrKind { None, String, Int, Long, Bool, Guid, Decimal, Double }

        // Text: the literal, or the query key. Prefix: the separator written before a path segment.
        private sealed record RouteOp(
            RouteOpKind Kind,
            string Prefix,
            string Text,
            string ParamId,
            ClrKind Clr,
            bool Optional,
            bool Conditional,
            bool NullableValue);

        private sealed class TemplatePart
        {
            public TemplatePart(bool isQuery, bool isPlaceholder, string text, string name, string type, bool isViewBound, bool optional)
            {
                IsQuery = isQuery;
                IsPlaceholder = isPlaceholder;
                Text = text;
                Name = name;
                Type = type;
                IsViewBound = isViewBound;
                Optional = optional;
            }

            public bool IsQuery { get; }
            public bool IsPlaceholder { get; }
            public string Text { get; }
            public string Name { get; }
            public string Type { get; }
            public bool IsViewBound { get; }
            public bool Optional { get; }

            public static TemplatePart Literal(bool isQuery, string text) =>
                new TemplatePart(isQuery, isPlaceholder: false, text, string.Empty, string.Empty, isViewBound: false, optional: false);
        }
    }
}