using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;

namespace Routeables.Tests.Generated;

public class RouteCoordinationGeneratorTests
{
    private const string Prelude = """
        using System;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Routeables.Abstraction;
        using Routeables.Routing;

        namespace Sample;

        public sealed class HomeViewModel { }
        public sealed class OtherViewModel { }
        public sealed class Animal(string name) { public string Name { get; } = name; }
        public sealed class PageView : IRouteableFor { public object? Context { get; set; } }
        """;

    // ---- generated shape ----------------------------------------------------------------

    [Fact]
    public void Generates_an_extension_method_on_the_declared_caller()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(IRouter router, Animal monkey, Action back) { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("internal static class Sample_HomeViewModelRouteCoordination", output.Generated);
        Assert.Contains("GoToMonkeyDetailAsync(", output.Generated);
        Assert.Contains("this global::Sample.HomeViewModel caller,", output.Generated);
        Assert.Contains("global::Sample.Animal monkey,", output.Generated);
        Assert.Contains("global::System.Action back,", output.Generated);
        Assert.Contains("router.GoToAsync(__route, __args, \"/monkeys\", cancellationToken)", output.Generated);

        // The IRouter constructor parameter is filled with the router the method is called with:
        // it is not asked from the caller a second time.
        Assert.Equal(1, CountOccurrences(output.Generated, "global::Routeables.Abstraction.IRouter router"));
        Assert.Contains("new global::Routeables.Abstraction.RouteableArgument(router)", output.Generated);
    }

    [Fact]
    public void The_method_is_only_visible_on_the_declared_caller()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel { }

            public sealed class Allowed
            {
                public Task Go(HomeViewModel home, IRouter router) => home.GoToMonkeyDetailAsync(router);
            }

            public sealed class Refused
            {
                public Task Go(OtherViewModel other, IRouter router) => other.GoToMonkeyDetailAsync(router);
            }
            """);

        var errors = output.CompileErrors.ToList();
        var error = Assert.Single(errors);
        // CS1929: the extension exists but its receiver is the declared caller, not this type.
        Assert.Equal("CS1929", error.Id);
        Assert.Contains("OtherViewModel", error.GetMessage());
    }

    [Fact]
    public void Each_declared_caller_gets_its_own_method()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel), typeof(OtherViewModel))]
            public sealed class MonkeyDetailViewModel { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("this global::Sample.HomeViewModel caller", output.Generated);
        Assert.Contains("this global::Sample.OtherViewModel caller", output.Generated);
        Assert.Equal(2, output.Run.GeneratedTrees.Length);
    }

    [Fact]
    public void Route_and_method_name_can_be_overridden()
    {
        var output = Generate(Prelude + """

            [Routeable("/a")]
            [Routeable("/b")]
            [RouteableViewModel(typeof(HomeViewModel), Route = "/b", MethodName = "OpenB")]
            public sealed class PickViewModel { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("OpenB(", output.Generated);
        Assert.Contains("__route, __args, \"/b\"", output.Generated);
    }

    [Fact]
    public void Template_wins_over_Route_and_defaults_to_it()
    {
        var output = Generate(Prelude + """

            [Routeable("/a")]
            [Routeable("/b")]
            [RouteableViewModel(typeof(HomeViewModel), Route = "/b", Template = "/c", MethodName = "OpenC")]
            [RouteableViewModel(typeof(HomeViewModel), Route = "/b", MethodName = "OpenB")]
            public sealed class PickViewModel { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("__route, __args, \"/c\"", output.Generated);
        Assert.Contains("__route, __args, \"/b\"", output.Generated);
        // Route is the string routed to, Template the one given to the router.
        Assert.Equal(2, CountOccurrences(output.Generated, "var __route = \"/b\";"));
        Assert.DoesNotContain("var __route = \"/c\";", output.Generated);
    }

    [Fact]
    public void Container_parameters_are_not_asked_from_the_caller()
    {
        var output = Generate(Prelude + """

            public interface IClock { }

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel([FromContainer] IClock clock, Animal monkey) { }
            """);

        Assert.DoesNotContain(output.GeneratorDiagnostics, d => d.Id != "ROUT106");
        Assert.Empty(output.CompileErrors);
        Assert.DoesNotContain("IClock", output.Generated);
        Assert.Contains("global::Sample.Animal monkey,", output.Generated);
    }

    [Fact]
    public void FromServices_also_marks_container_parameters()
    {
        var output = Generate(Prelude + """

            public interface IClock { }

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel([Microsoft.AspNetCore.Mvc.FromServices] IClock clock, Animal monkey) { }
            """);

        Assert.Empty(output.CompileErrors);
        Assert.DoesNotContain("IClock", output.Generated);
        Assert.Contains("global::Sample.Animal monkey,", output.Generated);
    }

    [Fact]
    public void Mandatory_parameters_are_checked_and_lose_their_nullable_annotation()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(Animal? monkey, int? visits) { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);

        // A null (or a nullable without value) cannot reach the container, so the signature does not pretend it can.
        Assert.Contains("global::Sample.Animal monkey,", output.Generated);
        Assert.DoesNotContain("Animal? monkey", output.Generated);
        Assert.Contains("int visits,", output.Generated);
        Assert.Contains("global::System.ArgumentNullException.ThrowIfNull(monkey);", output.Generated);
    }

    [Fact]
    public void Optional_parameters_keep_their_default_and_are_skipped_when_null()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(Animal monkey, string? note = null, int retries = 3) { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("string? note = null", output.Generated);
        Assert.Contains("int retries = 3", output.Generated);
        Assert.Contains("if (note is not null) __args.ForViewModel", output.Generated);
        Assert.DoesNotContain("if (retries is not null)", output.Generated);
    }

    [Fact]
    public void Keyword_parameter_names_are_escaped()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(Animal @event) { }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("global::Sample.Animal @event,", output.Generated);
    }

    [Fact]
    public void The_constructor_marked_for_ActivatorUtilities_is_the_one_mirrored()
    {
        var output = Generate(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel
            {
                public MonkeyDetailViewModel() { }

                [ActivatorUtilitiesConstructor]
                public MonkeyDetailViewModel(Animal monkey) { }

                public MonkeyDetailViewModel(Animal monkey, Animal other) { }
            }
            """);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);
        Assert.Contains("global::Sample.Animal monkey,", output.Generated);
        Assert.DoesNotContain("other", output.Generated);
    }

    // ---- end to end: the generated code compiles and routes through a real router --------

    [Fact]
    public async Task Generated_methods_route_through_a_real_router_with_the_constructor_arguments()
    {
        var summary = await RunScenarioAsync(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(IRouter router, Animal monkey, Action back, int? visits, string? note = null, int retries = 3)
            {
                public IRouter Router { get; } = router;
                public Animal Monkey { get; } = monkey;
                public Action Back { get; } = back;
                public int? Visits { get; } = visits;
                public string? Note { get; } = note;
                public int Retries { get; } = retries;
            }

            public static class Scenario
            {
                public static async Task<string> RunAsync()
                {
                    var services = new ServiceCollection();
                    services.RegisterRouting();
                    services.RegisterRoute<PageView, MonkeyDetailViewModel>(route: "/monkeys");
                    using var provider = services.BuildServiceProvider();
                    var router = provider.GetRequiredService<IRouter>();

                    var backCalled = false;
                    var result = await new HomeViewModel().GoToMonkeyDetailAsync(router, new Animal("Bubbles"), () => backCalled = true, 3);

                    var vm = (MonkeyDetailViewModel)router.CurrentViewModel!;
                    vm.Back();
                    return $"{result.Success}|{vm.Monkey.Name}|{ReferenceEquals(vm.Router, router)}|{backCalled}|{vm.Visits}|{vm.Note ?? "null"}|{vm.Retries}";
                }
            }
            """);

        Assert.Equal("True|Bubbles|True|True|3|null|3", summary);
    }

    [Fact]
    public async Task Route_placeholders_are_written_into_the_route_and_container_parameters_are_resolved()
    {
        var summary = await RunScenarioAsync(Prelude + """

            public interface IClock { string Now { get; } }
            public sealed class Clock : IClock { public string Now => "noon"; }

            [Routeable("/orders/{id:int}?{tab?}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class OrderViewModel([FromContainer] IClock clock, int id, string? tab = null)
            {
                public IClock Clock { get; } = clock;
                public int Id { get; } = id;
                public string? Tab { get; } = tab;
            }

            public static class Scenario
            {
                public static async Task<string> RunAsync()
                {
                    var services = new ServiceCollection();
                    services.RegisterRouting();
                    services.AddSingleton<IClock, Clock>();
                    services.RegisterRoute<PageView, OrderViewModel>(route: "/orders/{id:int}?{tab?}");
                    using var provider = services.BuildServiceProvider();
                    var router = provider.GetRequiredService<IRouter>();
                    var home = new HomeViewModel();

                    var first = await home.GoToOrderAsync(router, 42, "x y&z");
                    var one = (OrderViewModel)router.CurrentViewModel!;

                    var second = await home.GoToOrderAsync(router, 7);
                    var two = (OrderViewModel)router.CurrentViewModel!;

                    return $"{first.Success}|{one.Id}|{one.Tab}|{one.Clock.Now}|{second.Success}|{two.Id}|{two.Tab ?? "null"}";
                }
            }
            """);

        Assert.Equal("True|42|x y&z|noon|True|7|null", summary);
    }

    [Fact]
    public async Task Optional_trailing_path_placeholders_are_added_only_when_they_have_a_value()
    {
        var summary = await RunScenarioAsync(Prelude + """

            [Routeable("/files/{name}/{ext?}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class FileViewModel(string name, string? ext = null)
            {
                public string Name { get; } = name;
                public string? Ext { get; } = ext;
            }

            public static class Scenario
            {
                public static async Task<string> RunAsync()
                {
                    var services = new ServiceCollection();
                    services.RegisterRouting();
                    services.RegisterRoute<PageView, FileViewModel>(route: "/files/{name}/{ext?}");
                    using var provider = services.BuildServiceProvider();
                    var router = provider.GetRequiredService<IRouter>();
                    var home = new HomeViewModel();

                    var a = await home.GoToFileAsync(router, "my report");
                    var one = (FileViewModel)router.CurrentViewModel!;
                    var b = await home.GoToFileAsync(router, "my report", "pdf");
                    var two = (FileViewModel)router.CurrentViewModel!;

                    return $"{a.Success}|{one.Name}|{one.Ext ?? "null"}|{b.Success}|{two.Name}|{two.Ext}";
                }
            }
            """);

        Assert.Equal("True|my report|null|True|my report|pdf", summary);
    }

    [Fact]
    public async Task Typed_placeholders_are_formatted_with_the_invariant_culture()
    {
        var summary = await RunScenarioAsync(Prelude + """

            [Routeable("/items/{id:guid}/{flag:bool}/{ratio:double}/{price:decimal}/{count:long}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class ItemViewModel(Guid id, bool flag, double ratio, decimal price, long count)
            {
                public Guid Id { get; } = id;
                public bool Flag { get; } = flag;
                public double Ratio { get; } = ratio;
                public decimal Price { get; } = price;
                public long Count { get; } = count;
            }

            public static class Scenario
            {
                public static async Task<string> RunAsync()
                {
                    // A culture whose decimal separator is a comma: the route must still use a dot.
                    var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
                    culture.NumberFormat.NumberDecimalSeparator = ",";
                    System.Globalization.CultureInfo.CurrentCulture = culture;

                    var services = new ServiceCollection();
                    services.RegisterRouting();
                    services.RegisterRoute<PageView, ItemViewModel>(route: "/items/{id:guid}/{flag:bool}/{ratio:double}/{price:decimal}/{count:long}");
                    using var provider = services.BuildServiceProvider();
                    var router = provider.GetRequiredService<IRouter>();

                    var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
                    var result = await new HomeViewModel().GoToItemAsync(router, id, true, 1.5, 9.99m, -5L);
                    var vm = (ItemViewModel)router.CurrentViewModel!;

                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    return $"{result.Success}|{vm.Id}|{vm.Flag}|{vm.Ratio.ToString(inv)}|{vm.Price.ToString(inv)}|{vm.Count}";
                }
            }
            """);

        Assert.Equal("True|11111111-2222-3333-4444-555555555555|True|1.5|9.99|-5", summary);
    }

    [Fact]
    public async Task A_routing_method_fails_fast_on_a_missing_mandatory_argument()
    {
        var summary = await RunScenarioAsync(Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(Animal monkey) { }

            public static class Scenario
            {
                public static async Task<string> RunAsync()
                {
                    var services = new ServiceCollection();
                    services.RegisterRouting();
                    services.RegisterRoute<PageView, MonkeyDetailViewModel>(route: "/monkeys");
                    using var provider = services.BuildServiceProvider();
                    var router = provider.GetRequiredService<IRouter>();

                    try
                    {
                        await new HomeViewModel().GoToMonkeyDetailAsync(router, null!);
                        return "no exception";
                    }
                    catch (ArgumentNullException ex)
                    {
                        return ex.ParamName!;
                    }
                }
            }
            """);

        Assert.Equal("monkey", summary);
    }

    // ---- diagnostics --------------------------------------------------------------------

    [Fact]
    public void ROUT101_a_caller_is_required()
    {
        AssertDiagnostic("ROUT101", """

            [Routeable("/a")]
            [RouteableViewModel]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT101_a_static_class_cannot_be_a_caller()
    {
        AssertDiagnostic("ROUT101", """

            public static class Shell { }

            [Routeable("/a")]
            [RouteableViewModel(typeof(Shell))]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT101_a_generic_type_cannot_be_a_caller()
    {
        AssertDiagnostic("ROUT101", """

            public class Host<T> { }

            [Routeable("/a")]
            [RouteableViewModel(typeof(Host<int>))]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT102_an_abstract_destination_is_rejected()
    {
        AssertDiagnostic("ROUT102", """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public abstract class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT102_a_destination_without_public_constructor_is_rejected()
    {
        AssertDiagnostic("ROUT102", """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel { internal AViewModel() { } }
            """);
    }

    [Fact]
    public void ROUT102_ambiguous_constructors_are_rejected()
    {
        AssertDiagnostic("ROUT102", """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel
            {
                public AViewModel(Animal a) { }
                public AViewModel(HomeViewModel h) { }
            }
            """);
    }

    [Fact]
    public void ROUT102_an_invalid_method_name_is_rejected()
    {
        AssertDiagnostic("ROUT102", """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel), MethodName = "1st")]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT103_a_destination_needs_a_route()
    {
        AssertDiagnostic("ROUT103", """

            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT103_several_routes_need_an_explicit_choice()
    {
        AssertDiagnostic("ROUT103", """

            [Routeable("/a")]
            [Routeable("/b")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT104_a_placeholder_needs_a_constructor_parameter()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{id:int}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT104_a_container_parameter_cannot_receive_a_route_value()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{id:int}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel([FromContainer] int id) { }
            """);
    }

    [Fact]
    public void ROUT104_the_placeholder_type_must_match_the_parameter_type()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{id:int}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel(string id) { }
            """);
    }

    [Fact]
    public void ROUT104_an_optional_placeholder_needs_a_default_value()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{id:int?}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel(int id) { }
            """);
    }

    [Fact]
    public void ROUT104_an_optional_path_placeholder_must_be_last()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{x?}/b")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel(string? x = null) { }
            """);
    }

    [Fact]
    public void ROUT104_view_bound_placeholders_are_not_supported()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{animate::bool}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel { }
            """);
    }

    [Fact]
    public void ROUT104_custom_placeholder_types_are_not_supported()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/{who:Person}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel(Animal who) { }
            """);
    }

    [Fact]
    public void ROUT104_a_malformed_placeholder_is_rejected()
    {
        AssertDiagnostic("ROUT104", """

            [Routeable("/a/item-{id}")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel(int id) { }
            """);
    }

    [Fact]
    public void ROUT105_ref_parameters_are_rejected()
    {
        AssertDiagnostic("ROUT105", """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel
            {
                public AViewModel(ref int x) { }
            }
            """);
    }

    [Fact]
    public void ROUT105_a_parameter_cannot_reuse_a_name_of_the_generated_method()
    {
        AssertDiagnostic("ROUT105", """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel(Animal router) { }
            """);
    }

    [Fact]
    public void ROUT106_warns_when_the_container_could_swallow_an_argument()
    {
        var output = Generate(Prelude + """

            [Routeable("/a")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class AViewModel([FromContainer] Animal fromContainer, Animal fromCaller) { }
            """);

        var warning = Assert.Single(output.GeneratorDiagnostics);
        Assert.Equal("ROUT106", warning.Id);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);

        // It is only a warning: the method is still generated.
        Assert.Contains("GoToAAsync(", output.Generated);
    }

    [Fact]
    public void ROUT107_two_destinations_cannot_generate_the_same_method()
    {
        // Two destinations with the same simple name give the same default method name and parameters.
        var output = Generate(Prelude + """

            public static class First
            {
                [Routeable("/one")]
                [RouteableViewModel(typeof(HomeViewModel))]
                public sealed class DetailViewModel { }
            }

            public static class Second
            {
                [Routeable("/two")]
                [RouteableViewModel(typeof(HomeViewModel))]
                public sealed class DetailViewModel { }
            }
            """);

        Assert.Contains(output.GeneratorDiagnostics, d => d.Id == "ROUT107");
        Assert.Empty(output.CompileErrors);
    }

    [Fact]
    public void Generation_is_incremental_and_stable_for_identical_input()
    {
        const string source = Prelude + """

            [Routeable("/monkeys")]
            [RouteableViewModel(typeof(HomeViewModel))]
            public sealed class MonkeyDetailViewModel(IRouter router, Animal monkey) { }
            """;

        Assert.Equal(Generate(source).Generated, Generate(source).Generated);
    }

    // ---- helpers ------------------------------------------------------------------------

    private sealed record Output(GeneratorDriverRunResult Run, Compilation Compilation)
    {
        public string Generated => string.Join(Environment.NewLine, Run.GeneratedTrees.Select(tree => tree.ToString()));

        public IReadOnlyList<Diagnostic> GeneratorDiagnostics => Run.Diagnostics;

        public IEnumerable<Diagnostic> CompileErrors =>
            Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
    }

    private static void AssertDiagnostic(string id, string members)
    {
        var output = Generate(Prelude + members);

        Assert.Contains(output.GeneratorDiagnostics, d => d.Id == id);
        Assert.Empty(output.Generated);
    }

    private static Output Generate(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Routeables.Routing.Tests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new RouteCoordinationGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        return new Output(driver.GetRunResult(), outputCompilation);
    }

    /// <summary>Compiles the sample with the generated code, loads it and runs <c>Sample.Scenario.RunAsync</c>.</summary>
    private static async Task<string> RunScenarioAsync(string source)
    {
        var output = Generate(source);
        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);

        using var stream = new MemoryStream();
        var emitted = output.Compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));

        stream.Position = 0;
        var context = new AssemblyLoadContext("routing-scenario", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var scenario = assembly.GetType("Sample.Scenario") ?? throw new InvalidOperationException("Sample.Scenario is missing.");
            var run = scenario.GetMethod("RunAsync") ?? throw new InvalidOperationException("Scenario.RunAsync is missing.");
            return await (Task<string>)run.Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));

        return references.Concat([
            MetadataReference.CreateFromFile(typeof(RouteableAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Routeables.Routing.Extensions).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Mvc.FromServicesAttribute).Assembly.Location)
        ]).DistinctBy(reference => reference.Display);
    }
}