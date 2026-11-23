using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Generated;

namespace Routeables.Tests.Generated;

internal static class GeneratorReferences
{
    public static IEnumerable<MetadataReference> All() => AppDomain.CurrentDomain
        .GetAssemblies()
        .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
        .Select(assembly => assembly.Location)
        .Concat([
            typeof(RouteableAttribute).Assembly.Location,
            typeof(Routeables.Routing.Extensions).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
            typeof(ServiceCollectionContainerBuilderExtensions).Assembly.Location,
        ])
        .Distinct()
        .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location));
}

/// <summary>The edges of the registration generator: what it reads of attributes that are incomplete or odd, and what it writes for a type it cannot build.</summary>
public class RouteRegistrationGeneratorEdgeTests
{
    private sealed class RazorText(string path, string? text) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => text is null ? null : SourceText.From(text);
    }

    // The compilation is not required to be free of errors here: an attribute written without its argument is the case.
    private static GeneratorDriverRunResult Run(string source, IEnumerable<MetadataReference>? references = null, params AdditionalText[] texts)
    {
        var compilation = CSharpCompilation.Create(
            "Routeables.Generated.EdgeTests",
            [CSharpSyntaxTree.ParseText(source)],
            references ?? GeneratorReferences.All(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new RouteRegistrationGenerator().AsSourceGenerator()],
            additionalTexts: texts);
        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static string Code(GeneratorDriverRunResult result) => string.Join(Environment.NewLine, result.GeneratedTrees.Select(tree => tree.ToString()));

    private const string RazorWithAttribute = """
        @using Routeables.Abstraction
        @attribute [RouteableView(typeof(CatsViewModel), route: "/cats")]
        """;

    [Fact]
    public void A_project_without_the_routeables_attributes_gets_nothing_generated()
    {
        var result = Run("public class Plain { }", [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        Assert.Empty(result.GeneratedTrees.Where(t => t.ToString().Contains("RegisterRoute")));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void A_call_to_AddGeneratedRoutes_without_a_receiver_counts_for_the_razor_check()
    {
        var result = Run("""
            public static class Startup
            {
                private static void AddGeneratedRoutes() { }
                public static void Configure() { AddGeneratedRoutes(); }
            }
            """, null, new RazorText("Cats.razor", RazorWithAttribute));

        Assert.Single(result.Diagnostics, d => d.Id == "ROUT204");
    }

    [Fact]
    public void A_call_that_is_neither_a_member_nor_a_name_is_not_a_call_to_AddGeneratedRoutes()
    {
        var result = Run("""
            public static class Startup
            {
                private static System.Action Make() => () => { };
                public static void Configure() { Make()(); }
            }
            """, null, new RazorText("Cats.razor", RazorWithAttribute));

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "ROUT204");
    }

    [Fact]
    public void A_razor_file_that_has_no_text_is_skipped()
    {
        var result = Run("""
            public static class Startup
            {
                private static void AddGeneratedRoutes() { }
                public static void Configure() { AddGeneratedRoutes(); }
            }
            """, null, new RazorText("Empty.razor", null));

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "ROUT204");
    }

    [Fact]
    public void A_view_attribute_without_a_view_model_or_with_one_that_is_not_a_class_registers_nothing()
    {
        var result = Run("""
            using Routeables.Abstraction;

            [RouteableView] public class NoArgumentView { }
            [RouteableView(typeof(int[]))] public class ArrayView { }
            [Routeable("/fine")] public class FineViewModel { }
            """);

        var code = Code(result);
        Assert.DoesNotContain("NoArgumentView", code);
        Assert.DoesNotContain("ArrayView", code);
        Assert.Contains("/fine", code);
    }

    [Fact]
    public void The_region_and_the_lifetime_given_by_name_to_Routeable_are_used()
    {
        var result = Run("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            [Routeable("/side", Region = "sidebar", Lifetime = ServiceLifetime.Singleton)]
            public class SideViewModel { }
            """);

        var code = Code(result);
        Assert.Contains("services.AddRegion(\"sidebar\");", code);
        Assert.Contains("\"/side\"", code);
        Assert.Contains("ServiceLifetime.Singleton", code);
    }

    [Fact]
    public void A_view_model_with_several_constructors_is_registered_by_its_type_and_left_to_the_container_to_build()
    {
        var result = Run("""
            using Routeables.Abstraction;

            [Routeable("/multi")]
            public class MultiViewModel
            {
                public MultiViewModel() { }
                public MultiViewModel(int a) { }
            }
            """);

        var code = Code(result);
        Assert.Contains("MultiViewModel", code);
        Assert.Contains("/multi", code);
    }

    [Fact]
    public void A_view_with_no_route_anywhere_is_registered_with_its_lifetimes_only()
    {
        var result = Run("""
            using Routeables.Abstraction;

            public class PlainViewModel { }
            [RouteableView(typeof(PlainViewModel))] public class PlainView { }
            """);

        var code = Code(result);
        Assert.Contains("PlainViewModel", code);
        Assert.Contains("viewModelLifetime: ServiceLifetime.Transient, viewLifetime: ServiceLifetime.Transient", code);
        Assert.DoesNotContain(", route:", code);
    }

    [Fact]
    public void A_route_with_parameters_whose_view_alone_is_cached_is_reported_as_such()
    {
        var result = Run("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            public class CartViewModel { }
            [RouteableView(typeof(CartViewModel), "/cart/{id}", ViewLifetime = ServiceLifetime.Singleton)]
            public class CartView { }
            """);

        var warning = Assert.Single(result.Diagnostics, d => d.Id == "ROUT201");
        Assert.Contains("view (Singleton)", warning.GetMessage());
    }
}

/// <summary>The edges of the analyzer of route literals: what makes a route known, and what it leaves alone.</summary>
public class RouteLiteralAnalyzerEdgeTests
{
    private const string Declarations = """
        using System;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Routeables.Abstraction;
        using Routeables.Abstraction.Routing;
        [Routeable("/monkeys")] public class MonkeysViewModel { }
        """;

    private static async Task<string[]> Analyze(string statement, string extra = "", IEnumerable<MetadataReference>? references = null)
    {
        var source = Declarations + extra + $$"""

            public static class Caller
            {
                public static async Task Run(IRouter router, IServiceProvider provider, IServiceCollection services)
                {
                    {{statement}}
                }
            }
            """;

        var compilation = CSharpCompilation.Create(
            "Routeables.Analyzer.EdgeTests",
            [CSharpSyntaxTree.ParseText(source)],
            references ?? References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var diagnostics = await compilation.WithAnalyzers([new RouteLiteralAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return diagnostics.Select(d => d.Id).ToArray();
    }

    private static IEnumerable<MetadataReference> References()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is { } name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "mscorlib.dll" or "netstandard.dll"))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Concat([
                MetadataReference.CreateFromFile(typeof(IRouter).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location)])
            .DistinctBy(r => r.Display);

    [Fact]
    public async Task Without_the_routeables_assembly_there_is_nothing_to_check()
    {
        var framework = References().Where(r => !(r.Display ?? "").Contains("Routeables", StringComparison.Ordinal)
                                                && !(r.Display ?? "").Contains("DependencyInjection", StringComparison.Ordinal));
        var compilation = CSharpCompilation.Create(
            "NoRouteables",
            [CSharpSyntaxTree.ParseText("public class C { public void GoToAsync(string url) { } public void M() => GoToAsync(\"/x\"); }", cancellationToken: TestContext.Current.CancellationToken)],
            framework,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation.WithAnalyzers([new RouteLiteralAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task A_route_given_to_a_constructor_is_known()
    {
        const string extra = "public class Entry2 { public Entry2(string route) { } }";

        Assert.Empty(await Analyze("""new Entry2("/lonely"); await router.GoToAsync("/lonely");""", extra));
        Assert.Equal(["ROUT501"], await Analyze("""new Entry2("/lonely"); await router.GoToAsync("/lonelier");""", extra));
    }

    [Fact]
    public async Task A_route_assigned_to_a_property_named_Route_is_known()
    {
        const string extra = "public class Settable { public string? Route { get; set; } }";

        Assert.Empty(await Analyze("""new Settable().Route = "/assigned"; await router.GoToAsync("/assigned");""", extra));
    }

    [Fact]
    public async Task A_property_that_is_not_named_Route_or_whose_value_is_not_a_constant_is_not_a_route()
    {
        const string extra = """
            public class Other { public string Name => "/named"; }
            public class Dynamic { public string Route => DateTime.Now.ToString(); }
            """;

        Assert.Equal(["ROUT501"], await Analyze("""await router.GoToAsync("/named");""", extra));
    }

    [Fact]
    public async Task The_routes_of_types_in_a_namespace_and_nested_in_another_type_are_known()
    {
        const string extra = """
            namespace Shop { [Routeable("/in-namespace")] public class InNamespace { } }
            public class Outer { [Routeable("/nested")] public class Inner { } }
            """;

        Assert.Empty(await Analyze("""await router.GoToAsync("/in-namespace"); await router.GoToAsync("/nested");""", extra));
    }

    [Fact]
    public async Task A_type_that_implements_the_router_is_checked_like_the_router()
    {
        const string extra = """
            public class Mine : IRouter
            {
                public Task<RouteableResult> GoToAsync(string url, IRouteableArgs? args = null, string? template = null, System.Threading.CancellationToken cancellationToken = default)
                    => throw new NotImplementedException();
            }
            """;

        Assert.Equal(["ROUT501"], await Analyze("""await new Mine().GoToAsync("/nope");""", extra));
    }
}

/// <summary>The edges of the generator of the navigation methods: callers and destinations it cannot use.</summary>
public class RouteCoordinationGeneratorEdgeTests
{
    private const string Prelude = """
        using System;
        using Routeables.Abstraction;

        namespace Sample;

        public sealed class HomeViewModel { }
        """;

    private static GeneratorDriverRunResult Generate(string members)
    {
        var compilation = CSharpCompilation.Create(
            "Routeables.Routing.EdgeTests",
            [CSharpSyntaxTree.ParseText(Prelude + members)],
            GeneratorReferences.All(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new RouteCoordinationGenerator());
        return driver.RunGenerators(compilation).GetRunResult();
    }

    [Theory]
    [InlineData("[RouteableViewModel(typeof(int[]))] public sealed class Dest { }", "int[]")]
    [InlineData("[RouteableViewModel(typeof(DayOfWeek))] public sealed class Dest { }", "it must be a class, struct or interface")]
    public void A_caller_that_is_not_a_class_a_struct_or_an_interface_is_refused(string members, string said)
    {
        var result = Generate(members);

        var error = Assert.Single(result.Diagnostics, d => d.Id == "ROUT101");
        Assert.Contains(said, error.GetMessage());
    }

    [Fact]
    public void A_caller_that_is_not_accessible_from_the_assembly_is_refused()
    {
        var result = Generate("""
            public class Holder
            {
                private sealed class Hidden { }
                [RouteableViewModel(typeof(Hidden))] public sealed class Dest { }
            }
            """);

        Assert.Contains("not accessible", Assert.Single(result.Diagnostics, d => d.Id == "ROUT101").GetMessage());
    }

    [Fact]
    public void A_caller_that_does_not_exist_is_left_to_the_compiler_to_report()
    {
        var result = Generate("[RouteableViewModel(typeof(Nowhere))] public sealed class Dest { }");

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "ROUT101");
    }

    [Fact]
    public void A_generic_destination_is_refused()
    {
        var result = Generate("[RouteableViewModel(typeof(HomeViewModel))] public sealed class Dest<T> { }");

        Assert.Contains("cannot be generic", Assert.Single(result.Diagnostics, d => d.Id == "ROUT102").GetMessage());
    }

    [Fact]
    public void A_destination_that_is_not_accessible_is_refused()
    {
        var result = Generate("""
            public class Holder
            {
                [RouteableViewModel(typeof(HomeViewModel))] private sealed class Dest { }
            }
            """);

        Assert.Contains("must be public or internal", Assert.Single(result.Diagnostics, d => d.Id == "ROUT102").GetMessage());
    }
}
