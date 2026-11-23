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
        private const string ViewModelSuffix = "ViewModel";
        private const string ViewSuffix = "View";

        private static readonly SymbolDisplayFormat FqName = SymbolDisplayFormat.FullyQualifiedFormat;

        // A Scoped or Singleton instance is cached by route and built once, with the arguments of the first navigation, so a route that takes
        // parameters would show the first value forever.
        private static readonly DiagnosticDescriptor CachedRouteWithParameters = new DiagnosticDescriptor(
            "ROUT201",
            "Cached route with parameters",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout201");

        // A [Routeable] view model without a [RouteableView] is paired with the view its name says (OrderViewModel => OrderView), but when two
        // views have that name the choice is left to the author.
        private static readonly DiagnosticDescriptor AmbiguousConventionView = new DiagnosticDescriptor(
            "ROUT202",
            "Several views match the naming convention",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout202");

        // Two views registered for the same view model, route and region replace one another: the last one registered is the one the router builds.
        // This happens when an assembly brings the views of several hosts, or when a view is left behind after a copy.
        private static readonly DiagnosticDescriptor SeveralViewsForOneRoute = new DiagnosticDescriptor(
            "ROUT203",
            "Several views for the same view model and route",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout203");

        // Two view models declare the same route (and region): the route table keeps the first, so the second is never reached, and the
        // run-time message calls it a route that is not registered.
        private static readonly DiagnosticDescriptor SeveralViewModelsForOneRoute = new DiagnosticDescriptor(
            "ROUT206",
            "Several view models for the same route",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout206");

        // A [Routeable] class the generated code cannot name (open generic, 'file' type) is left out with this warning, rather than generating code that does not compile.
        private static readonly DiagnosticDescriptor UnusableViewModel = new DiagnosticDescriptor(
            "ROUT207",
            "[Routeable] on a type that cannot be registered",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout207");

        // A .razor file declaring [RouteableView] with @attribute: the Razor compiler is another generator, and generators do not see each
        // other's output, so nothing is registered. Reported only when this project calls its own AddGeneratedRoutes(); a component library
        // whose views are registered by the referencing application is fine.
        private static readonly DiagnosticDescriptor RazorViewAttributeIgnored = new DiagnosticDescriptor(
            "ROUT204",
            "[RouteableView] in a .razor file is ignored here",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout204");

        // A [Routeable] view model in a project with views of its own, none named after it: most often a name that breaks the convention.
        // Information only, since a view model without a view is a legitimate route (a redirect, a headless one).
        // Switch it off with .editorconfig (dotnet_diagnostic.ROUT205.severity = none) or #pragma warning disable ROUT205.
        private static readonly DiagnosticDescriptor ConventionViewNotFound = new DiagnosticDescriptor(
            "ROUT205",
            "No view found for a view model",
            "{0}",
            "Routeables",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true, helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#rout205");

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

            // 1) The current compilation (syntax). AllowMultiple means iterating over ALL attributes.
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

            // 2) The whole compilation and the referenced assemblies.
            var fromCompilation = context.CompilationProvider
                .Select(static (compilation, ct) => Compile(compilation, ct))
                .WithTrackingName("RouteablesCompiled");

            // 3) Whether the registration is written by a module initializer.
            var autoRegister = RegisterAutoRegisterReport(context);

            var combined = localViews.Collect()
                .Combine(localRoutes.Collect())
                .Combine(fromCompilation)
                .Combine(autoRegister);

            context.RegisterSourceOutput(combined, static (spc, data) =>
                Merge(spc, data.Left.Left.Left, data.Left.Left.Right, data.Left.Right, data.Right));
        }

        // What the types of the compilation and of the assemblies it references hold: the views, the routes and the views that the naming
        // convention may pair with a view model.
        private sealed class Scanned
        {
            public List<ViewReg> Views { get; } = [];
            public List<Route> Routes { get; } = [];
            public List<INamedTypeSymbol> ConventionViews { get; } = [];
            public List<IAssemblySymbol> PlainReferences { get; } = [];
        }

        private static CompiledModel Compile(Compilation compilation, System.Threading.CancellationToken ct)
        {
            var routeableAttr = compilation.GetTypeByMetadataName(RouteableAttrMetadataName);
            var routeableViewAttr = compilation.GetTypeByMetadataName(RouteableViewAttrMetadataName);
            if (routeableAttr is null && routeableViewAttr is null)
                return CompiledModel.Empty;

            var scanned = ScanTypes(compilation, routeableAttr, routeableViewAttr, ct);
            var wanted = ViewsWantedByViewModels(scanned.Routes);
            AddConventionViewsOfReferences(compilation, scanned, routeableAttr, wanted, ct);
            var candidates = DescribeCandidates(compilation, scanned.ConventionViews, wanted, ct);

            // Values that compare equal when their content is equal, so the pipeline sees an unchanged model after an edit that changes none of the
            // views and routes, and does not generate the file again.
            return new CompiledModel(
                new EquatableArray<ViewReg>(scanned.Views.ToArray()),
                new EquatableArray<Route>(scanned.Routes.ToArray()),
                new EquatableArray<ViewCandidate>(candidates.ToArray()));
        }

        // The views and the routes that the attributes of the types declare.
        private static Scanned ScanTypes(
            Compilation compilation, INamedTypeSymbol? routeableAttr, INamedTypeSymbol? routeableViewAttr, System.Threading.CancellationToken ct)
        {
            var scanned = new Scanned();

            // A type carries [Routeable] or [RouteableView] only in an assembly that references the one holding the attributes. The other
            // referenced assemblies (control libraries, mapping, logging...) are only searched, below, for views that view models name, cheaply:
            // by type name, without creating a symbol for each type.
            var attributeAssemblies = new HashSet<string>(StringComparer.Ordinal);
            if (routeableAttr is not null) attributeAssemblies.Add(routeableAttr.ContainingAssembly.Name);
            if (routeableViewAttr is not null) attributeAssemblies.Add(routeableViewAttr.ContainingAssembly.Name);

            foreach (var type in GetAllTypes(compilation, attributeAssemblies, scanned.PlainReferences))
            {
                ct.ThrowIfCancellationRequested();

                // A view the naming convention may pair with a [Routeable] view model: a concrete class called ...View. It need not implement
                // anything (a UserControl, a Page, a component): its name pairs it. It is described below only for views a view model names.
                if (routeableAttr is not null && IsConventionView(type) && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                    scanned.ConventionViews.Add(type);

                foreach (var attr in type.GetAttributes())
                {
                    if (routeableViewAttr is not null &&
                        SymbolEqualityComparer.Default.Equals(attr.AttributeClass, routeableViewAttr))
                    {
                        var reg = FromViewAttribute(compilation, type, attr);
                        if (reg is not null)
                            scanned.Views.Add(reg);
                    }
                    else if (routeableAttr is not null &&
                             SymbolEqualityComparer.Default.Equals(attr.AttributeClass, routeableAttr))
                    {
                        var route = FromRouteableAttribute(compilation, type, attr);
                        if (route is not null)
                            scanned.Routes.Add(route);
                    }
                }
            }

            return scanned;
        }

        // The names of the views that the view models are paired with: CatsViewModel is shown by CatsView.
        private static HashSet<string> ViewsWantedByViewModels(List<Route> routes) =>
            new(
                routes.Where(r => r.Name.Length > ViewModelSuffix.Length && r.Name.EndsWith(ViewModelSuffix, StringComparison.Ordinal))
                      .Select(r => r.Name.Substring(0, r.Name.Length - ViewModelSuffix.Length) + ViewSuffix),
                StringComparer.Ordinal);

        // A view the view models name may live in a referenced assembly that has no attribute: only the assemblies that hold one of those names are searched.
        private static void AddConventionViewsOfReferences(
            Compilation compilation, Scanned scanned, INamedTypeSymbol? routeableAttr, HashSet<string> wanted, System.Threading.CancellationToken ct)
        {
            foreach (var assembly in scanned.PlainReferences)
            {
                ct.ThrowIfCancellationRequested();
                if (routeableAttr is null || !assembly.TypeNames.Any(wanted.Contains))
                    continue;
                foreach (var type in GetTypesInNamespace(assembly.GlobalNamespace))
                    if (wanted.Contains(type.Name) && IsConventionView(type) && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                        scanned.ConventionViews.Add(type);
            }
        }

        private static List<ViewCandidate> DescribeCandidates(
            Compilation compilation, List<INamedTypeSymbol> conventionViews, HashSet<string> wanted, System.Threading.CancellationToken ct)
        {
            var candidates = new List<ViewCandidate>();
            foreach (var type in conventionViews)
            {
                ct.ThrowIfCancellationRequested();
                candidates.Add(new ViewCandidate(
                    Name: type.Name,
                    FullName: type.ToDisplayString(FqName),
                    IsLocal: SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly),
                    Build: wanted.Contains(type.Name) ? RouteBuilds.Analyze(compilation, type) : null));
            }

            return candidates;
        }

        // An executable hands its routes to the host through a module initializer, so the application need not call AddGeneratedRoutes().
        // RouteablesAutoRegister=false switches this off; =true switches it on for a project the compiler does not see as an executable (a
        // mobile app, say). A library has no such initializer: its module would be initialized only when one of its types is first used,
        // which is too late and unpredictable.
        private static bool ShouldAutoRegister(OutputKind kind, string? setting) =>
            string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase)
            || (IsExecutable(kind) && !string.Equals(setting, "false", StringComparison.OrdinalIgnoreCase));

        // Whether the registration is written by a module initializer, and a file that says so: when an application finds no route, it says why
        // the registration was not written.
        private static IncrementalValueProvider<bool> RegisterAutoRegisterReport(IncrementalGeneratorInitializationContext context)
        {
            var autoInputs = context.CompilationProvider
                .Select(static (compilation, _) => compilation.Options.OutputKind)
                .Combine(context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
                    options.GlobalOptions.TryGetValue("build_property.RouteablesAutoRegister", out var value) ? value?.Trim() : null));

            context.RegisterSourceOutput(autoInputs, static (spc, pair) =>
                spc.AddSource("RouteablesAutoRegister.g.cs", SourceText.From(
                    $"// OutputKind: {pair.Left}; RouteablesAutoRegister: {pair.Right ?? "(not set)"}; registration written: {ShouldAutoRegister(pair.Left, pair.Right)}\n",
                    Encoding.UTF8)));

            return autoInputs.Select(static (pair, _) => ShouldAutoRegister(pair.Left, pair.Right));
        }

        // Puts what the attributes of the current compilation and what the whole compilation found together, reports the routes that cannot be used,
        // and generates the registration.
        private static void Merge(
            SourceProductionContext spc, IEnumerable<ViewReg> localV, IEnumerable<Route> localR, CompiledModel compiled, bool autoRegisterRoutes)
        {
            var viewsByKey = new Dictionary<string, ViewReg>(StringComparer.Ordinal);
            var routesByKey = new Dictionary<string, Route>(StringComparer.Ordinal);

            // The same view and view model pair may appear with different routes and regions.
            void AddView(ViewReg v) =>
                viewsByKey[v.ViewFullName + "|" + v.ViewModelFullName + "|" + v.Route + "|" + v.Region] = v;

            // Several [Routeable] attributes per type are allowed.
            void AddRoute(Route r) =>
                routesByKey[r.TypeName + "|" + r.Path + "|" + r.Region] = r;

            foreach (var v in compiled.Views) AddView(v);
            foreach (var r in compiled.Routes) AddRoute(r);
            foreach (var v in localV) AddView(v);
            foreach (var r in localR) AddRoute(r);

            foreach (var bad in routesByKey.Values.Where(r => r.Problem is not null).OrderBy(r => r.TypeName, StringComparer.Ordinal))
                spc.ReportDiagnostic(Diagnostic.Create(UnusableViewModel, Location.None,
                    "[Routeable] on " + bad.TypeName + " is ignored: " + bad.Problem));

            Generate(spc, viewsByKey.Values, routesByKey.Values.Where(r => r.Problem is null).ToList(), compiled.Candidates.ToList(), autoRegisterRoutes);
        }

        private static bool IsConventionView(INamedTypeSymbol type) =>
            type.TypeKind == TypeKind.Class
            && !type.IsAbstract
            && !type.IsGenericType
            && type.Name.Length > ViewSuffix.Length
            && type.Name.EndsWith(ViewSuffix, StringComparison.Ordinal);

        // The assemblies of frameworks and big libraries: none of their types is a view of the application. A name is matched whole or followed by
        // a dot: "AvaloniaShop.Features", "PresentationLayer" and "SystemMonitor.Pages" are the application's own libraries.
        private static readonly string[] FrameworkAssemblies =
        [
            "System", "Microsoft", "Avalonia", "WindowsBase", "Uno", "WinRT", "MudBlazor",
            // WPF: PresentationFramework has a ListView, a TreeView, a GridView...
            "PresentationFramework", "PresentationCore", "PresentationUI",
            // The Android and iOS bindings and the graphics libraries: tens of thousands of types, none of them a view of the application.
            "Mono.Android", "Xamarin", "Java.Interop", "SkiaSharp", "HarfBuzzSharp",
            "mscorlib", "netstandard",
        ];

        internal static bool IsFrameworkAssembly(string name)
        {
            foreach (var framework in FrameworkAssemblies)
                if (name.Equals(framework, StringComparison.Ordinal)
                    || name.Length > framework.Length && name[framework.Length] == '.' && name.StartsWith(framework, StringComparison.Ordinal))
                    return true;
            return false;
        }

        // The types of the assembly being compiled and of the referenced assemblies that can hold a [Routeable] type (those that reference the
        // attributes' assembly). The others, except the frameworks, go in plainReferences.
        private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation, HashSet<string> attributeAssemblies, List<IAssemblySymbol> plainReferences)
        {
            // The assembly being compiled only: compilation.GlobalNamespace merges this assembly with every reference (the framework included), which
            // the loop below would then walk a second time.
            foreach (var t in GetTypesInNamespace(compilation.Assembly.GlobalNamespace))
                yield return t;

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm)
                    continue;

                // Skip obvious framework and NuGet assemblies to limit the cost.
                if (IsFrameworkAssembly(asm.Name))
                    continue;

                if (!asm.Modules.Any(module => module.ReferencedAssemblies.Any(identity => attributeAssemblies.Contains(identity.Name))))
                {
                    plainReferences.Add(asm);
                    continue;
                }

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

            // Region and Route are get-only on the attribute, so only the lifetimes can be named arguments.
            // ViewModelLifetime and ViewLifetime, when given, win over Lifetime whatever order they are written in.
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
            var lifetime = ServiceLifetime.Transient;

            if (attr.ConstructorArguments.Length > 0)
                path = attr.ConstructorArguments[0].Value as string;

            if (attr.ConstructorArguments.Length > 1)
                region = attr.ConstructorArguments[1].Value as string;

            // The lifetime is the third constructor argument (it was the fourth when the attribute still had a contract, which made a lifetime given to
            // the constructor be ignored).
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
                Lifetime: lifetime,
                Build: RouteBuilds.Analyze(compilation, symbol),
                IsLocal: SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly),
                Problem: ProblemOf(compilation, symbol));
        }

        // A view model the generated code cannot name: an open generic type (OrderViewModel<T>), a type local to its file, or one it cannot reach.
        // Registering it would write code that does not compile, so it is reported and left out.
        private static string? ProblemOf(Compilation compilation, INamedTypeSymbol symbol)
        {
            if (!compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly))
                return "the type is not accessible from this project (private, or internal in another assembly), so the generated code could not name it. Make it public or internal.";
            for (var t = symbol; t is not null; t = t.ContainingType)
            {
                if (t.IsGenericType)
                    return "an open generic type cannot be registered: no type argument is known. Register a closed type, or write the registration by hand.";
                if (t.IsFileLocal)
                    return "a type declared 'file' cannot be named by the generated code. Make it internal or public.";
            }
            return null;
        }


        private static void Generate(
            SourceProductionContext spc,
            IEnumerable<ViewReg> viewsIn,
            IEnumerable<Route> routesIn,
            IReadOnlyList<ViewCandidate> candidates,
            bool autoRegister)
        {
            var viewList = viewsIn.ToList();
            AddConventionViews(spc, viewList, routesIn.ToList(), candidates);
            ReportSeveralViews(spc, viewList);
            ReportSeveralViewModels(spc, routesIn.ToList());

            // Deterministic output.
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

            var source = new RegistrationWriter().Write(views, routes, routesByType, autoRegister);
            spc.AddSource("RouteRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private static bool IsExecutable(OutputKind kind) =>
            kind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication;

        private static void ReportSeveralViewModels(SourceProductionContext spc, List<Route> routes)
        {
            // The same address as the route table reads it: the case and a trailing slash do not tell two routes apart.
            var groups = routes
                .Where(r => !string.IsNullOrEmpty(r.Path))
                .GroupBy(r => (Path: r.Path!.Trim().TrimEnd('/').ToLowerInvariant(), Region: r.Region ?? string.Empty))
                .OrderBy(g => g.Key.Path, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var names = group.Select(r => r.TypeName).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (names.Count < 2)
                    continue;

                spc.ReportDiagnostic(Diagnostic.Create(
                    SeveralViewModelsForOneRoute,
                    Location.None,
                    names.Count + " view models declare the route '" + group.First().Path + "' (" + string.Join(", ", names) + "): only the first registered is reached. Give each its own route."));
            }
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

        // A [Routeable] view model that no [RouteableView] names gets the view its name implies: OrderViewModel => OrderView, a routed class (one
        // that implements IRouteable) found in the project or, failing that, in the assemblies it references. The route, region and lifetime come
        // from the [Routeable] attribute. When two views have that name, it does not guess.
        private static void AddConventionViews(
            SourceProductionContext spc,
            List<ViewReg> views,
            List<Route> routes,
            IReadOnlyList<ViewCandidate> candidates)
        {
            if (candidates.Count == 0)
                return;

            // Only a project that has views of its own can have forgotten one: a library of view models, whose views are in each host's projects, has
            // nothing to report.
            var hasLocalViews = candidates.Any(c => c.IsLocal);
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
                else if (chosen.Count == 0 && hasLocalViews && first.IsLocal)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        ConventionViewNotFound,
                        Location.None,
                        group.Key + " is registered without a view: no class is called " + viewName + ", and no [RouteableView(typeof(" + group.Key + "))] names one. Rename the view, or put [RouteableView] on it; ignore this if the route has no view."));
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

            public string Write(List<ViewReg> views, List<Route> routes, ILookup<string, Route> routesByType, bool autoRegister)
            {
                _sb.AppendLine("// <auto-generated />");
                _sb.AppendLine("#nullable enable");
                // The injectors set [Parameter] properties of Blazor components, which is how framework-free injection has always worked (the reflection
                // path does the same); the Blazor analyzer would reject it in generated code.
                _sb.AppendLine("#pragma warning disable BL0005");
                _sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
                _sb.AppendLine("using Routeables.Abstraction;");
                _sb.AppendLine("using Routeables.Abstraction.Bases;");
                _sb.AppendLine("using Routeables.Abstraction.Lifecycle;");
                _sb.AppendLine("using Routeables.Abstraction.Registrations;");
                _sb.AppendLine("using Routeables.Abstraction.Routing;");
                _sb.AppendLine("using Routeables.Routing;");
                _sb.AppendLine();
                _sb.AppendLine("namespace Routeables.Generated;");
                _sb.AppendLine();
                _sb.AppendLine("internal static class RouteRegistration");
                _sb.AppendLine("{");
                if (autoRegister)
                {
                    // The host applies these routes after the application's services, unless AddGeneratedRoutes() was called.
                    _sb.AppendLine("#pragma warning disable CA2255");
                    _sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
                    _sb.AppendLine("#pragma warning restore CA2255");
                    _sb.AppendLine("    internal static void RegisterWithHost() => RouteableAutoRegistration.Add(typeof(RouteRegistration).Assembly, static services => AddGeneratedRoutes(services));");
                    _sb.AppendLine();
                }
                _sb.AppendLine("    public static IServiceCollection AddGeneratedRoutes(this IServiceCollection services)");
                _sb.AppendLine("    {");
                _sb.AppendLine("        RouteableAutoRegistration.Mark(services, typeof(RouteRegistration).Assembly);");

                // A) View and view model pairs ([RouteableView]); route and region come from the view and/or [Routeable] on the view model.
                foreach (var view in views)
                    WritePair(view, routesByType[view.ViewModelFullName].ToList());

                // B) [Routeable] types without a [RouteableView]: no view, register the type alone.
                var viewModelsWithView = new HashSet<string>(views.Select(v => v.ViewModelFullName), StringComparer.Ordinal);
                foreach (var route in routes.Where(r => !viewModelsWithView.Contains(r.TypeName)))
                    WriteRouteOnly(route);

                // How the injection fills the types that were described, known to it before the provider is built.
                foreach (var description in _descriptions.All)
                    _sb.AppendLine($"        global::Routeables.Abstraction.Injections.RouteableBuild.Describe(services, typeof({description.Info.TypeName}), {description.Name}());");

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

                // A region declared on the view is always registered, even without a route.
                WriteRegion(view.Region);

                if (hasViewRoute)
                    WritePairCall(view, ToLifetimeLiteral(view.ViewModelLifetime), ToLifetimeLiteral(view.ViewLifetime), view.Route);

                foreach (var route in viewModelRoutes)
                {
                    // The same path is already registered from the view attribute.
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

        // The text of a C# string literal, without its quotes: a new line or any control character is escaped too.
        private static string Escape(string value) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: false);

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

        // What the compilation holds, with value equality (see the Select that builds it).
        private sealed record CompiledModel(
            EquatableArray<ViewReg> Views,
            EquatableArray<Route> Routes,
            EquatableArray<ViewCandidate> Candidates)
        {
            public static readonly CompiledModel Empty = new(default, default, default);
        }

        private sealed record ViewCandidate(string Name, string FullName, bool IsLocal, RouteBuilds.BuildInfo? Build);

        private sealed record Route(
            string TypeName,
            string Name,
            string? Path,
            string? Region,
            ServiceLifetime Lifetime,
            RouteBuilds.BuildInfo? Build = null,
            bool IsLocal = false,
            string? Problem = null);
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}