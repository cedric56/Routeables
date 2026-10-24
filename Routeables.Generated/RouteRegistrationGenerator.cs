using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Routeables.Generated
{

    [Generator]
    public sealed class RouteRegistrationGenerator : IIncrementalGenerator
    {
        private const string RouteableAttrMetadataName = "Routeables.Abstraction.RouteableAttribute";
        private const string RouteableViewAttrMetadataName = "Routeables.Abstraction.RouteableViewAttribute";
        private const string RouteableInterfaceMetadataName = "Routeables.Abstraction.IRouteable";
        private const string ViewModelSuffix = "ViewModel";
        private const string ViewSuffix = "View";

        private static readonly SymbolDisplayFormat FqName = SymbolDisplayFormat.FullyQualifiedFormat;

        // A Scoped or Singleton instance is cached by route and built once, with the arguments of the first route change:
        // a route that takes parameters would show the first value for ever.
        private static readonly DiagnosticDescriptor CachedRouteWithParameters = new DiagnosticDescriptor(
            "ROUT201",
            "Cached route with parameters",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // A [Routeable] view model with no [RouteableView] is paired with the view the name says (OrderViewModel => OrderView), but
        // two views of that name leave the choice to the author.
        private static readonly DiagnosticDescriptor AmbiguousConventionView = new DiagnosticDescriptor(
            "ROUT202",
            "Several views match the naming convention",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // Two views registered for the same view model, route and region replace one another: the last registered is the one the
        // router builds. It happens when an assembly brings the views of several hosts, or when a view is left behind after a copy.
        private static readonly DiagnosticDescriptor SeveralViewsForOneRoute = new DiagnosticDescriptor(
            "ROUT203",
            "Several views for the same view model and route",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // A .razor file of the project that declares [RouteableView] with @attribute: the Razor compiler is another generator, and a
        // generator does not see the output of another one, so nothing is registered. Only reported when this project calls its own
        // AddGeneratedRoutes(): a component library whose views are registered by the application that references it is fine.
        private static readonly DiagnosticDescriptor RazorViewAttributeIgnored = new DiagnosticDescriptor(
            "ROUT204",
            "[RouteableView] in a .razor file is ignored here",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly System.Text.RegularExpressions.Regex RazorViewAttribute = new System.Text.RegularExpressions.Regex(
            @"^[ \t]*@attribute[ \t]*\[[ \t]*(?:Routeables\.Abstraction\.)?RouteableView(?:Attribute)?\b",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static void InitializeRazorCheck(IncrementalGeneratorInitializationContext context)
        {
            var callsAddGeneratedRoutes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is InvocationExpressionSyntax invocation && CallsAddGeneratedRoutes(invocation),
                    transform: static (_, _) => true)
                .Collect();

            var razorAttributes = context.AdditionalTextsProvider
                .Where(static text => text.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                .SelectMany(static (text, ct) => FindRazorViewAttributes(text, ct))
                .Collect();

            context.RegisterSourceOutput(callsAddGeneratedRoutes.Combine(razorAttributes), static (spc, data) =>
            {
                if (data.Left.Length == 0)
                    return;

                foreach (var found in data.Right)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        RazorViewAttributeIgnored,
                        Location.Create(found.Path, found.Span, found.LineSpan),
                        "This .razor file declares [RouteableView], but the generator cannot read the .razor files of the project that calls "
                        + "AddGeneratedRoutes(): the route is not registered. Put [Routeable] on the view model (the view is found by its name), "
                        + "move the view to a referenced project, or register it with RegisterRoute."));
                }
            });
        }

        private static bool CallsAddGeneratedRoutes(InvocationExpressionSyntax invocation) =>
            invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "AddGeneratedRoutes",
                IdentifierNameSyntax name => name.Identifier.ValueText == "AddGeneratedRoutes",
                _ => false,
            };

        private static IEnumerable<RazorAttributeLocation> FindRazorViewAttributes(AdditionalText text, System.Threading.CancellationToken ct)
        {
            var source = text.GetText(ct);
            if (source is null)
                yield break;

            var content = source.ToString();
            foreach (System.Text.RegularExpressions.Match match in RazorViewAttribute.Matches(content))
            {
                var span = new TextSpan(match.Index, match.Length);
                yield return new RazorAttributeLocation(text.Path, span, source.Lines.GetLinePositionSpan(span));
            }
        }

        private readonly record struct RazorAttributeLocation(string Path, TextSpan Span, LinePositionSpan LineSpan);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            InitializeRazorCheck(context);

            // 1) Current compilation (syntax). AllowMultiple => iterate ALL attributes.
            var localViews = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    RouteableViewAttrMetadataName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => GetViewRegistrations(ctx))
                .SelectMany(static (x, _) => x);

            var localRoutes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    RouteableAttrMetadataName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => GetRoutes(ctx))
                .SelectMany(static (x, _) => x);

            // 2) Whole compilation + referenced assemblies
            var fromCompilation = context.CompilationProvider.Select(static (compilation, ct) =>
            {
                var routeableAttr = compilation.GetTypeByMetadataName(RouteableAttrMetadataName);
                var routeableViewAttr = compilation.GetTypeByMetadataName(RouteableViewAttrMetadataName);
                var routeableInterface = compilation.GetTypeByMetadataName(RouteableInterfaceMetadataName);

                var views = new List<ViewReg>();
                var routes = new List<Route>();
                var candidates = new List<ViewCandidate>();

                if (routeableAttr is null && routeableViewAttr is null)
                    return (views: (IReadOnlyList<ViewReg>)views, routes: (IReadOnlyList<Route>)routes, candidates: (IReadOnlyList<ViewCandidate>)candidates);

                foreach (var type in GetAllTypes(compilation))
                {
                    ct.ThrowIfCancellationRequested();

                    // A view the naming convention may pair with a [Routeable] view model: a concrete class called ...View that is routed.
                    if (routeableAttr is not null && routeableInterface is not null && IsConventionView(type, routeableInterface))
                    {
                        candidates.Add(new ViewCandidate(
                            Name: type.Name,
                            FullName: type.ToDisplayString(FqName),
                            IsLocal: SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly),
                            Build: RouteBuilds.Analyze(compilation, type)));
                    }

                    foreach (var attr in type.GetAttributes())
                    {
                        if (routeableViewAttr is not null &&
                            SymbolEqualityComparer.Default.Equals(attr.AttributeClass, routeableViewAttr))
                        {
                            var reg = FromViewAttribute(compilation, type, attr);
                            if (reg is not null)
                                views.Add(reg);
                        }
                        else if (routeableAttr is not null &&
                                 SymbolEqualityComparer.Default.Equals(attr.AttributeClass, routeableAttr))
                        {
                            var route = FromRouteableAttribute(compilation, type, attr);
                            if (route is not null)
                                routes.Add(route);
                        }
                    }
                }

                return (views: (IReadOnlyList<ViewReg>)views, routes: (IReadOnlyList<Route>)routes, candidates: (IReadOnlyList<ViewCandidate>)candidates);
            });

            var combined = localViews.Collect()
                .Combine(localRoutes.Collect())
                .Combine(fromCompilation);

            context.RegisterSourceOutput(combined, static (spc, data) =>
            {
                var localV = data.Left.Left;
                var localR = data.Left.Right;
                var compiled = data.Right;

                var viewsByKey = new Dictionary<string, ViewReg>(StringComparer.Ordinal);
                var routesByKey = new Dictionary<string, Route>(StringComparer.Ordinal);

                // Same view/viewmodel pair may appear with different routes/regions
                void AddView(ViewReg v) =>
                    viewsByKey[v.ViewFullName + "|" + v.ViewModelFullName + "|" + v.Route + "|" + v.Region] = v;

                // Multiple [Routeable] per type are allowed
                void AddRoute(Route r) =>
                                        //routesByKey[r.TypeName + "|" + r.Path + "|" + r.Region + "|" + r.Contract] = r;
                                        routesByKey[r.TypeName + "|" + r.Path + "|" + r.Region] = r;

                foreach (var v in compiled.views) AddView(v);
                foreach (var r in compiled.routes) AddRoute(r);
                foreach (var v in localV) AddView(v);
                foreach (var r in localR) AddRoute(r);

                Generate(spc, viewsByKey.Values, routesByKey.Values, compiled.candidates);
            });
        }

        // ------------------------------------------------------------------ symbol helpers

        private static bool IsConventionView(INamedTypeSymbol type, INamedTypeSymbol routeableInterface) =>
            type.TypeKind == TypeKind.Class
            && !type.IsAbstract
            && !type.IsGenericType
            && type.Name.Length > ViewSuffix.Length
            && type.Name.EndsWith(ViewSuffix, StringComparison.Ordinal)
            && type.AllInterfaces.Contains(routeableInterface, SymbolEqualityComparer.Default);

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
        {
            foreach (var t in GetTypesInNamespace(compilation.GlobalNamespace))
                yield return t;

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm)
                    continue;

                // Skip obvious framework / NuGet assemblies to limit cost
                var name = asm.Name;
                if (name.StartsWith("System", StringComparison.Ordinal) ||
                    name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                    name.StartsWith("Avalonia", StringComparison.Ordinal) ||
                    name.StartsWith("MudBlazor", StringComparison.Ordinal) ||
                    name.StartsWith("netstandard", StringComparison.Ordinal))
                    continue;

                foreach (var t in GetTypesInNamespace(asm.GlobalNamespace))
                    yield return t;
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetTypesInNamespace(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                yield return type;
                foreach (var nested in GetNestedTypes(type))
                    yield return nested;
            }

            foreach (var child in ns.GetNamespaceMembers())
                foreach (var type in GetTypesInNamespace(child))
                    yield return type;
        }

        private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
        {
            foreach (var nested in type.GetTypeMembers())
            {
                yield return nested;
                foreach (var n2 in GetNestedTypes(nested))
                    yield return n2;
            }
        }

        // ------------------------------------------------------------------ attribute → models

        private static ImmutableArray<ViewReg> GetViewRegistrations(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol viewSymbol)
                return ImmutableArray<ViewReg>.Empty;

            var builder = ImmutableArray.CreateBuilder<ViewReg>(ctx.Attributes.Length);
            foreach (var attr in ctx.Attributes)
            {
                var reg = FromViewAttribute(ctx.SemanticModel.Compilation, viewSymbol, attr);
                if (reg is not null)
                    builder.Add(reg);
            }
            return builder.ToImmutable();
        }

        private static ImmutableArray<Route> GetRoutes(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
                return ImmutableArray<Route>.Empty;

            var builder = ImmutableArray.CreateBuilder<Route>(ctx.Attributes.Length);
            foreach (var attr in ctx.Attributes)
            {
                var route = FromRouteableAttribute(ctx.SemanticModel.Compilation, symbol, attr);
                if (route is not null)
                    builder.Add(route);
            }
            return builder.ToImmutable();
        }

        private static ViewReg? FromViewAttribute(Compilation compilation, INamedTypeSymbol viewSymbol, AttributeData attr)
        {
            // [RouteableView(typeof(HomeViewModel), "route", "region", ServiceLifetime.Transient)]
            if (attr.ConstructorArguments.Length == 0)
                return null;

            if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol vmType)
                return null;

            string? route = null;
            string? region = null;
            var lifetime = ServiceLifetime.Transient;

            if (attr.ConstructorArguments.Length > 1)
                route = attr.ConstructorArguments[1].Value as string;

            if (attr.ConstructorArguments.Length > 2)
                region = attr.ConstructorArguments[2].Value as string;

            if (attr.ConstructorArguments.Length > 3 &&
                attr.ConstructorArguments[3].Value is int lifeCtor)
                lifetime = (ServiceLifetime)lifeCtor;

            // Region and Route are get-only on the attribute: only the lifetimes can be named arguments.
            // ViewModelLifetime and ViewLifetime, when given, win over Lifetime whatever the order they are written in.
            ServiceLifetime? viewModelLifetime = null;
            ServiceLifetime? viewLifetime = null;
            foreach (var named in attr.NamedArguments)
            {
                if (named.Value.Value is not int lifeNamed)
                    continue;

                switch (named.Key)
                {
                    case "Lifetime":
                        lifetime = (ServiceLifetime)lifeNamed;
                        break;
                    case "ViewModelLifetime":
                        viewModelLifetime = (ServiceLifetime)lifeNamed;
                        break;
                    case "ViewLifetime":
                        viewLifetime = (ServiceLifetime)lifeNamed;
                        break;
                }
            }

            return new ViewReg(
                ViewFullName: viewSymbol.ToDisplayString(FqName),
                ViewModelFullName: vmType.ToDisplayString(FqName),
                Route: route,
                Region: region,
                ViewModelLifetime: viewModelLifetime ?? lifetime,
                ViewLifetime: viewLifetime ?? lifetime,
                Location: RegLocation.From(attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()),
                ViewBuild: RouteBuilds.Analyze(compilation, viewSymbol),
                ViewModelBuild: RouteBuilds.Analyze(compilation, vmType));
        }

        private static Route? FromRouteableAttribute(Compilation compilation, INamedTypeSymbol symbol, AttributeData attr)
        {
            // [Routeable("path", region, serviceLifetime)]
            string? path = null;
            string? region = null;
            //string? contract = null;
            var lifetime = ServiceLifetime.Transient;

            if (attr.ConstructorArguments.Length > 0)
                path = attr.ConstructorArguments[0].Value as string;

            if (attr.ConstructorArguments.Length > 1)
                region = attr.ConstructorArguments[1].Value as string;

            // The lifetime is the third constructor argument (it was the fourth when the attribute still had a contract,
            // which made a lifetime given to the constructor be ignored).
            if (attr.ConstructorArguments.Length > 2 &&
                attr.ConstructorArguments[2].Value is int lifeCtor)
                lifetime = (ServiceLifetime)lifeCtor;

            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "Region" when named.Value.Value is string r:
                        region = r;
                        break;
                    //case "Contract" when named.Value.Value is string c:
                    //    contract = c;
                    //    break;
                    case "Lifetime" when named.Value.Value is int lifeNamed:
                        lifetime = (ServiceLifetime)lifeNamed;
                        break;
                }
            }

            return new Route(
                TypeName: symbol.ToDisplayString(FqName),
                Name: symbol.Name,
                Path: path,
                Region: region,
                //Contract: contract,
                Lifetime: lifetime,
                Build: RouteBuilds.Analyze(compilation, symbol));
        }

        // ------------------------------------------------------------------ emit

        private static void Generate(
            SourceProductionContext spc,
            IEnumerable<ViewReg> viewsIn,
            IEnumerable<Route> routesIn,
            IReadOnlyList<ViewCandidate> candidates)
        {
            var viewList = viewsIn.ToList();
            AddConventionViews(spc, viewList, routesIn.ToList(), candidates);
            ReportSeveralViews(spc, viewList);

            // Deterministic output
            var views = viewList
                .OrderBy(v => v.ViewFullName, StringComparer.Ordinal)
                .ThenBy(v => v.ViewModelFullName, StringComparer.Ordinal)
                .ThenBy(v => v.Route, StringComparer.Ordinal)
                .ThenBy(v => v.Region, StringComparer.Ordinal)
                .ToList();

            var routes = routesIn
                .OrderBy(r => r.TypeName, StringComparer.Ordinal)
                .ThenBy(r => r.Path, StringComparer.Ordinal)
                .ToList();

            var routesByType = routes.ToLookup(r => r.TypeName, StringComparer.Ordinal);

            foreach (var v in views)
                ReportCachedRoutes(spc, v, routesByType[v.ViewModelFullName]);

            var source = new RegistrationWriter().Write(views, routes, routesByType);
            spc.AddSource("RouteRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private static void ReportSeveralViews(SourceProductionContext spc, List<ViewReg> views)
        {
            var groups = views
                .GroupBy(v => (v.ViewModelFullName, Route: v.Route ?? string.Empty, Region: v.Region ?? string.Empty))
                .OrderBy(g => g.Key.ViewModelFullName, StringComparer.Ordinal)
                .ThenBy(g => g.Key.Route, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var names = group.Select(v => v.ViewFullName).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (names.Count < 2)
                    continue;

                var route = group.Key.Route.Length > 0 ? " on the route '" + group.Key.Route + "'" : string.Empty;
                spc.ReportDiagnostic(Diagnostic.Create(
                    SeveralViewsForOneRoute,
                    group.Select(v => v.Location?.ToLocation()).FirstOrDefault(l => l is not null) ?? Location.None,
                    names.Count + " views are registered for " + group.Key.ViewModelFullName + route + " (" + string.Join(", ", names) + "): the last registered replaces the others. Keep one view for it, or give the others another route or region."));
            }
        }

        // A [Routeable] view model that no [RouteableView] names gets the view its name says: OrderViewModel => OrderView, a routed class
        // (it implements IRouteable) found in the project, or failing that in the assemblies it references. The route, region and
        // lifetime are the ones of the [Routeable] attribute. Two views of that name are not guessed between.
        private static void AddConventionViews(
            SourceProductionContext spc,
            List<ViewReg> views,
            List<Route> routes,
            IReadOnlyList<ViewCandidate> candidates)
        {
            if (candidates.Count == 0)
                return;

            var named = new HashSet<string>(views.Select(v => v.ViewModelFullName), StringComparer.Ordinal);
            foreach (var group in routes.GroupBy(r => r.TypeName, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                if (named.Contains(group.Key))
                    continue;

                var first = group.First();
                if (first.Name.Length <= ViewModelSuffix.Length || !first.Name.EndsWith(ViewModelSuffix, StringComparison.Ordinal))
                    continue;

                var viewName = first.Name.Substring(0, first.Name.Length - ViewModelSuffix.Length) + ViewSuffix;
                var matches = candidates.Where(c => string.Equals(c.Name, viewName, StringComparison.Ordinal)).ToList();
                var local = matches.Where(c => c.IsLocal).ToList();
                var chosen = (local.Count > 0 ? local : matches).GroupBy(c => c.FullName, StringComparer.Ordinal).Select(g => g.First()).ToList();

                if (chosen.Count == 1)
                {
                    views.Add(new ViewReg(
                        ViewFullName: chosen[0].FullName,
                        ViewModelFullName: group.Key,
                        Route: null,
                        Region: null,
                        ViewModelLifetime: ServiceLifetime.Transient,
                        ViewLifetime: ServiceLifetime.Transient,
                        ViewBuild: chosen[0].Build,
                        ViewModelBuild: first.Build));
                }
                else if (chosen.Count > 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        AmbiguousConventionView,
                        Location.None,
                        group.Key + " has no [RouteableView] and several views are called " + viewName + " (" + string.Join(", ", chosen.Select(c => c.FullName).OrderBy(n => n, StringComparer.Ordinal)) + "). Put [RouteableView(typeof(" + group.Key + "))] on the one you mean."));
                }
            }
        }

        // The methods the generated class gets for the types it builds: one per type, however many routes the type has.
        private sealed class BuildMethods(string prefix)
        {
            private readonly Dictionary<string, (string Name, RouteBuilds.BuildInfo Info)> _methods = new(StringComparer.Ordinal);

            public IEnumerable<(string Name, RouteBuilds.BuildInfo Info)> All => _methods.Values;

            public string NameOf(RouteBuilds.BuildInfo info)
            {
                if (!_methods.TryGetValue(info.TypeName, out var known))
                {
                    known = (prefix + _methods.Count, info);
                    _methods[info.TypeName] = known;
                }
                return known.Name;
            }
        }

        private sealed class RegistrationWriter
        {
            private readonly StringBuilder _sb = new();
            private readonly HashSet<string> _regions = new(StringComparer.Ordinal);
            private readonly BuildMethods _views = new("View");
            private readonly BuildMethods _viewModels = new("ViewModel");
            private readonly BuildMethods _descriptions = new("Describe");

            public string Write(List<ViewReg> views, List<Route> routes, ILookup<string, Route> routesByType)
            {
                _sb.AppendLine("// <auto-generated />");
                _sb.AppendLine("#nullable enable");
                // The injectors set [Parameter] properties of Blazor components, which is how the framework-free injection has always
                // worked (the reflection path does the same); the Blazor analyzer would refuse it in generated code.
                _sb.AppendLine("#pragma warning disable BL0005");
                _sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
                _sb.AppendLine("using Routeables.Abstraction;");
                _sb.AppendLine("using Routeables.Routing;");
                _sb.AppendLine();
                _sb.AppendLine("namespace Routeables.Generated;");
                _sb.AppendLine();
                _sb.AppendLine("internal static class RouteRegistration");
                _sb.AppendLine("{");
                _sb.AppendLine("    public static IServiceCollection AddGeneratedRoutes(this IServiceCollection services)");
                _sb.AppendLine("    {");

                // A) View + ViewModel pairs ([RouteableView]); route/region from the view and/or [Routeable] on the ViewModel
                foreach (var view in views)
                    WritePair(view, routesByType[view.ViewModelFullName].ToList());

                // B) [Routeable] types with no [RouteableView] => no view, register the type alone
                var viewModelsWithView = new HashSet<string>(views.Select(v => v.ViewModelFullName), StringComparer.Ordinal);
                foreach (var route in routes.Where(r => !viewModelsWithView.Contains(r.TypeName)))
                    WriteRouteOnly(route);

                // How the injection fills the types that were described, known to it before the provider is built.
                foreach (var description in _descriptions.All)
                    _sb.AppendLine($"        global::Routeables.Abstraction.RouteableBuild.Describe(services, typeof({description.Info.TypeName}), {description.Name}());");

                _sb.AppendLine("        return services;");
                _sb.AppendLine("    }");
                _sb.AppendLine();

                foreach (var method in _views.All)
                    RouteBuilds.EmitFactory(_sb, method.Name, method.Info, viewModel: false);
                foreach (var method in _viewModels.All)
                    RouteBuilds.EmitFactory(_sb, method.Name, method.Info, viewModel: true);
                foreach (var description in _descriptions.All)
                    RouteBuilds.EmitDescription(_sb, description.Name, description.Info);

                _sb.AppendLine("}");
                return _sb.ToString();
            }

            private void WritePair(ViewReg view, List<Route> viewModelRoutes)
            {
                var hasViewRoute = !string.IsNullOrEmpty(view.Route);

                // Region declared on the view is always registered, even without a route
                WriteRegion(view.Region);

                if (hasViewRoute)
                    WritePairCall(view, ToLifetimeLiteral(view.ViewModelLifetime), ToLifetimeLiteral(view.ViewLifetime), view.Route);

                foreach (var route in viewModelRoutes)
                {
                    // Same path already registered from the view attribute
                    if (hasViewRoute && string.Equals(route.Path, view.Route, StringComparison.Ordinal))
                        continue;

                    WriteRegion(route.Region);
                    var life = ToLifetimeLiteral(route.Lifetime);
                    WritePairCall(view, life, life, route.Path);
                }

                if (!hasViewRoute && viewModelRoutes.Count == 0)
                    WritePairCall(view, ToLifetimeLiteral(view.ViewModelLifetime), ToLifetimeLiteral(view.ViewLifetime), null);
            }

            private void WritePairCall(ViewReg view, string viewModelLifetime, string viewLifetime, string? route)
            {
                var call = $"        services.RegisterRoute<{view.ViewFullName}, {view.ViewModelFullName}>(viewModelLifetime: {viewModelLifetime}, viewLifetime: {viewLifetime}{RouteArg(route)}";
                if (view.ViewBuild is { CanConstruct: true } viewBuild && view.ViewModelBuild is { CanConstruct: true } viewModelBuild)
                    call += $", viewFactory: {_views.NameOf(viewBuild)}, viewModelFactory: {_viewModels.NameOf(viewModelBuild)}";
                Describe(view.ViewBuild);
                Describe(view.ViewModelBuild);
                _sb.AppendLine(call + ");");
            }

            private void WriteRouteOnly(Route route)
            {
                WriteRegion(route.Region);
                var life = ToLifetimeLiteral(route.Lifetime);
                Describe(route.Build);
                if (route.Build is { CanConstruct: true } built && !string.IsNullOrEmpty(route.Path))
                    _sb.AppendLine($"        services.RegisterRoute(\"{Escape(route.Path!)}\", {_views.NameOf(built)}, {life});");
                else
                    _sb.AppendLine($"        services.RegisterRoute<{route.TypeName}>(lifetime: {life}{RouteArg(route.Path)});");
            }

            private void WriteRegion(string? region)
            {
                if (!string.IsNullOrWhiteSpace(region) && _regions.Add(region!))
                    _sb.AppendLine($"        services.AddRegion(\"{Escape(region!)}\");");
            }

            private void Describe(RouteBuilds.BuildInfo? info)
            {
                if (info is { CanDescribe: true })
                    _descriptions.NameOf(info);
            }
        }

        private static void ReportCachedRoutes(SourceProductionContext spc, ViewReg view, IEnumerable<Route> viewModelRoutes)
        {
            void Check(string? path, ServiceLifetime viewModelLifetime, ServiceLifetime viewLifetime)
            {
                if (path is null || path.IndexOf('{') < 0)
                    return;

                string? cached = null;
                if (viewModelLifetime != ServiceLifetime.Transient)
                    cached = "view model (" + viewModelLifetime + ")";
                else if (viewLifetime != ServiceLifetime.Transient)
                    cached = "view (" + viewLifetime + ")";

                if (cached is null)
                    return;

                spc.ReportDiagnostic(Diagnostic.Create(
                    CachedRouteWithParameters,
                    view.Location?.ToLocation() ?? Location.None,
                    "The route '" + path + "' has parameters but its " + cached + " is cached by route: it is built once, with the arguments of the first route change, and the later ones do not pass theirs. Use ServiceLifetime.Transient for " + view.ViewFullName + "."));
            }

            Check(view.Route, view.ViewModelLifetime, view.ViewLifetime);
            foreach (var route in viewModelRoutes)
            {
                if (!string.Equals(route.Path, view.Route, StringComparison.Ordinal))
                    Check(route.Path, route.Lifetime, route.Lifetime);
            }
        }

        private static string RouteArg(string? path) =>
            string.IsNullOrEmpty(path) ? "" : $", route: \"{Escape(path!)}\"";

        private static string ToLifetimeLiteral(ServiceLifetime lifetime) => lifetime switch
        {
            ServiceLifetime.Singleton => "ServiceLifetime.Singleton",
            ServiceLifetime.Scoped => "ServiceLifetime.Scoped",
            _ => "ServiceLifetime.Transient",
        };

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private enum ServiceLifetime
        {
            Singleton = 0,
            Scoped = 1,
            Transient = 2,
        }

        private sealed record ViewReg(
            string ViewFullName,
            string ViewModelFullName,
            string? Route,
            string? Region,
            ServiceLifetime ViewModelLifetime,
            ServiceLifetime ViewLifetime,
            RegLocation? Location = null,
            RouteBuilds.BuildInfo? ViewBuild = null,
            RouteBuilds.BuildInfo? ViewModelBuild = null);

        private sealed record RegLocation(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
        {
            public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

            public static RegLocation? From(Location? location)
            {
                if (location is null || location.SourceTree is null)
                    return null;

                return new RegLocation(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
            }
        }

        private sealed record ViewCandidate(string Name, string FullName, bool IsLocal, RouteBuilds.BuildInfo? Build);

        private sealed record Route(
            string TypeName,
            string Name,
            string? Path,
            string? Region,
            //string? Contract,
            ServiceLifetime Lifetime,
            RouteBuilds.BuildInfo? Build = null);
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}