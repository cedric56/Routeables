using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using Routeables.Routing;

namespace Routeables.Tests.Generated;

public class RouteRegistrationGeneratorTests
{
    [Fact]
    public void Generator_registers_a_route_when_only_the_path_is_provided()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            [Routeable("/simple")]
            public class SimpleViewModel
            {
            }

            [RouteableView(typeof(SimpleViewModel))]
            public class SimpleView
            {
            }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("route: \"/simple\"", generated);
        Assert.DoesNotContain("AddRegion(", generated);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Generator_gives_the_view_and_its_view_model_their_own_lifetime()
    {
        var result = RunGenerator("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            public class CartViewModel
            {
            }

            [RouteableView(typeof(CartViewModel), "/cart", ViewModelLifetime = ServiceLifetime.Singleton)]
            public class CartView
            {
            }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("viewModelLifetime: ServiceLifetime.Singleton, viewLifetime: ServiceLifetime.Transient, route: \"/cart\"", generated);
    }

    [Fact]
    public void Generator_applies_Lifetime_to_both_unless_one_is_given_apart()
    {
        var result = RunGenerator("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            public class AViewModel { }
            public class BViewModel { }

            [RouteableView(typeof(AViewModel), "/a", serviceLifetime: ServiceLifetime.Scoped)]
            public class AView { }

            [RouteableView(typeof(BViewModel), "/b", ViewLifetime = ServiceLifetime.Singleton, Lifetime = ServiceLifetime.Scoped)]
            public class BView { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("viewModelLifetime: ServiceLifetime.Scoped, viewLifetime: ServiceLifetime.Scoped, route: \"/a\"", generated);
        Assert.Contains("viewModelLifetime: ServiceLifetime.Scoped, viewLifetime: ServiceLifetime.Singleton, route: \"/b\"", generated);
    }

    [Fact]
    public void RouteableView_attribute_lifetimes_fall_back_to_Lifetime()
    {
        var attribute = new RouteableViewAttribute(typeof(object), serviceLifetime: ServiceLifetime.Scoped);

        Assert.Equal(ServiceLifetime.Scoped, attribute.ViewModelLifetime);
        Assert.Equal(ServiceLifetime.Scoped, attribute.ViewLifetime);

        attribute.ViewModelLifetime = ServiceLifetime.Singleton;

        Assert.Equal(ServiceLifetime.Singleton, attribute.ViewModelLifetime);
        Assert.Equal(ServiceLifetime.Scoped, attribute.ViewLifetime);
        Assert.Equal(ServiceLifetime.Scoped, attribute.Lifetime);
    }

    [Fact]
    public void Generator_warns_when_a_route_with_parameters_is_cached()
    {
        var result = RunGenerator("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            public class DogViewModel { }

            [RouteableView(typeof(DogViewModel), "/dogs/{name}", serviceLifetime: ServiceLifetime.Scoped)]
            public class DogView { }
            """);

        var warning = Assert.Single(result.Diagnostics, d => d.Id == "ROUT201");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("/dogs/{name}", warning.GetMessage());
    }

    [Fact]
    public void Generator_does_not_warn_for_a_transient_route_or_a_cached_route_without_parameters()
    {
        var result = RunGenerator("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            public class DogViewModel { }
            public class CartViewModel { }

            [RouteableView(typeof(DogViewModel), "/dogs/{name}")]
            public class DogView { }

            [RouteableView(typeof(CartViewModel), "/cart", serviceLifetime: ServiceLifetime.Singleton)]
            public class CartView { }
            """);

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "ROUT201");
    }

    [Fact]
    public void Generator_uses_the_lifetime_given_to_the_Routeable_constructor()
    {
        var result = RunGenerator("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            [Routeable("/cart", serviceLifetime: ServiceLifetime.Singleton)]
            public class CartViewModel { }

            [RouteableView(typeof(CartViewModel))]
            public class CartView { }
            """);

        Assert.Contains(
            "viewModelLifetime: ServiceLifetime.Singleton, viewLifetime: ServiceLifetime.Singleton, route: \"/cart\"",
            GetGeneratedCode(result));
    }

    [Fact]
    public void Generator_builds_the_view_and_its_view_model_without_reflection_when_they_have_one_constructor()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            public class CatViewModel { public CatViewModel(IRouter router, int age = 3) { } }

            [RouteableView(typeof(CatViewModel), "/cat/{age:int?}")]
            public class CatView { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("viewFactory:", generated);
        Assert.Contains("viewModelFactory:", generated);
        Assert.Contains("RouteableBuild.Describe(", generated);
    }

    [Fact]
    public void Generated_code_may_set_the_parameters_of_a_Blazor_component()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            public class PandaViewModel { }

            [RouteableView(typeof(PandaViewModel), "/panda")]
            public class PandaView { }
            """);

        Assert.Contains("#pragma warning disable BL0005", GetGeneratedCode(result));
    }

    [Fact]
    public void Generator_leaves_a_view_model_with_several_constructors_to_the_container()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            public class TwoViewModel { public TwoViewModel() { } public TwoViewModel(int n) { } }

            [RouteableView(typeof(TwoViewModel), "/two")]
            public class TwoView { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.DoesNotContain("viewModelFactory:", generated);
        Assert.Contains("route: \"/two\"", generated);
    }

    [Fact]
    public async Task Generated_registration_routes_and_injects_like_the_reflection_one()
    {
        const string source = """
            using Routeables.Abstraction;

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class ParamAttribute : System.Attribute { }

            public interface IGreeter { string Hi(); }
            public class Greeter : IGreeter { public string Hi() => "hi"; }

            public class CatViewModel
            {
                public CatViewModel(IGreeter greeter, int age = 3) { Greeter = greeter; Age = age; }
                public IGreeter Greeter { get; }
                public int Age { get; }
                [Param] public string? Nick { get; set; }
            }

            [RouteableView(typeof(CatViewModel), "/cat/{age:int?}/{Nick::?}")]
            public class CatView { public object? Context { get; set; } }

            public static class Setup
            {
                public static void Generated(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    => Routeables.Generated.RouteRegistration.AddGeneratedRoutes(services);

                public static void Manual(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    => Routeables.Abstraction.Extensions.RegisterRoute<CatView, CatViewModel>(services, route: "/cat/{age:int?}/{Nick::?}");
            }
            """;

        var (generated, reflection) = (await Run(useGenerated: true), await Run(useGenerated: false));

        Assert.Equal("hi/7/Tom", generated);
        Assert.Equal(reflection, generated);

        async Task<string> Run(bool useGenerated)
        {
            var assembly = Emit(source);
            var services = new ServiceCollection();
            services.RegisterRouting();
            services.AddSingleton<IInjection>(new PropertyInjection(new RouteablePropertyEntry(assembly.GetType("ParamAttribute")!, FromContainer: false)));
            var greeter = assembly.GetType("Greeter")!;
            services.AddSingleton(assembly.GetType("IGreeter")!, greeter);
            assembly.GetType("Setup")!.GetMethod(useGenerated ? "Generated" : "Manual")!.Invoke(null, [services]);

            using var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<IRouter>();
            var routing = await router.GoToAsync("/cat/7/Tom", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(routing.Success, routing.Exception?.Message);

            var viewModel = router.CurrentViewModel!;
            var type = viewModel.GetType();
            return $"{((dynamic)type.GetProperty("Greeter")!.GetValue(viewModel)!).Hi()}/{type.GetProperty("Age")!.GetValue(viewModel)}/{type.GetProperty("Nick")!.GetValue(viewModel)}";
        }
    }

    private static System.Reflection.Assembly Emit(string source, params MetadataReference[] more)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Routeables.Generated.Tests." + Guid.NewGuid().ToString("N"),
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences().Concat(more),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new RouteRegistrationGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return System.Reflection.Assembly.Load(stream.ToArray());
    }

    private const string LibraryWithInternalInjection = """
        using Microsoft.Extensions.DependencyInjection;
        using Routeables.Abstraction;

        [System.AttributeUsage(System.AttributeTargets.Property)]
        public class InjAttribute : System.Attribute { }

        internal sealed class ModalService { public string Name => "service"; }

        public static class LibraryServices
        {
            public static void Add(IServiceCollection services) => services.AddScoped<ModalService>();
        }

        // Like a component base class of a library: its injected property is internal.
        public class BaseView : IRouteableFor
        {
            public object Context { get; set; }
            [Inj] internal ModalService Modals { get; set; }
            public string Dump() => Modals == null ? "none" : Modals.Name;
        }
        """;

    private const string ApplicationOverInternalInjection = """
        using Routeables.Abstraction;

        public sealed class ModalViewModel { }

        [RouteableView(typeof(ModalViewModel), "/modal")]
        public class ModalView : BaseView { }

        public static class Setup
        {
            public static void Generated(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                => Routeables.Generated.RouteRegistration.AddGeneratedRoutes(services);

            public static void Manual(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                => Routeables.Abstraction.Extensions.RegisterRoute<ModalView, ModalViewModel>(services, route: "/modal");
        }
        """;

    [Fact]
    public void Generator_leaves_a_type_whose_base_class_is_in_another_assembly_to_reflection()
    {
        // The compiler shows the generator only the public members of another assembly: an internal property of the base
        // class is invisible to it, so describing the view would silently leave that property out.
        var library = EmitLibrary(LibraryWithInternalInjection);

        var generated = GetGeneratedCode(RunGenerator(ApplicationOverInternalInjection, library.Reference));

        Assert.DoesNotContain("typeof(global::ModalView)", generated);   // not described: found by reflection
        Assert.Contains("typeof(global::ModalViewModel)", generated);    // no base class outside: still described
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_internal_property_of_a_base_class_in_another_assembly_is_injected(bool useGenerated)
    {
        var library = EmitLibrary(LibraryWithInternalInjection);
        var resolve = new ResolveEventHandler((_, args) =>
            args.Name!.StartsWith(library.Assembly.GetName().Name!, StringComparison.Ordinal) ? library.Assembly : null);
        AppDomain.CurrentDomain.AssemblyResolve += resolve;
        try
        {
            var application = Emit(ApplicationOverInternalInjection, library.Reference);

            var services = new ServiceCollection();
            services.RegisterRouting();
            library.Assembly.GetType("LibraryServices")!.GetMethod("Add")!.Invoke(null, [services]);
            services.AddSingleton<IInjection>(new PropertyInjection(new RouteablePropertyEntry(library.Assembly.GetType("InjAttribute")!)));
            application.GetType("Setup")!.GetMethod(useGenerated ? "Generated" : "Manual")!.Invoke(null, [services]);

            using var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<IRouter>();
            var routing = await router.GoToAsync("/modal", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(routing.Success, routing.Exception?.Message);

            var view = router.CurrentView!;
            Assert.Equal("service", view.GetType().GetMethod("Dump")!.Invoke(view, null));
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolve;
        }
    }

    /// <summary>Compiles a library the application code of a test refers to, as a separate assembly, with no generator.</summary>
    private static (System.Reflection.Assembly Assembly, MetadataReference Reference) EmitLibrary(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Routeables.Generated.Library." + Guid.NewGuid().ToString("N"),
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        var image = stream.ToArray();
        return (System.Reflection.Assembly.Load(image), MetadataReference.CreateFromImage(image));
    }

    [Fact]
    public void Generator_pairs_a_Routeable_view_model_with_the_view_its_name_says()
    {
        var result = RunGenerator("""
            using Microsoft.Extensions.DependencyInjection;
            using Routeables.Abstraction;

            [Routeable("/orders/{id:int}", region: "detail", serviceLifetime: ServiceLifetime.Transient)]
            public class OrderViewModel(int id) { public int Id { get; } = id; }

            public class OrderView : IRouteable { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("RegisterRoute<global::OrderView, global::OrderViewModel>(", generated);
        Assert.Contains("route: \"/orders/{id:int}\"", generated);
        Assert.Contains("AddRegion(\"detail\")", generated);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Generator_does_not_pair_a_class_called_View_that_is_not_routed()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            [Routeable("/orders")]
            public class OrderViewModel { }

            public class OrderView { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.DoesNotContain("global::OrderView,", generated);
        Assert.Contains("OrderViewModel", generated);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Generator_leaves_the_view_to_RouteableView_when_the_view_model_is_named_by_one()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            [Routeable("/orders")]
            public class OrderViewModel { }

            [RouteableView(typeof(OrderViewModel))]
            public class ListOfOrders : IRouteable { }

            public class OrderView : IRouteable { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("RegisterRoute<global::ListOfOrders, global::OrderViewModel>(", generated);
        Assert.DoesNotContain("global::OrderView,", generated);
    }

    [Fact]
    public void Generator_does_not_guess_between_two_views_of_the_same_name()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            [Routeable("/orders")]
            public class OrderViewModel { }

            namespace A { public class OrderView : IRouteable { } }
            namespace B { public class OrderView : IRouteable { } }
            """);

        var generated = GetGeneratedCode(result);

        var warning = Assert.Single(result.Diagnostics, d => d.Id == "ROUT202");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("A.OrderView", warning.GetMessage());
        Assert.Contains("B.OrderView", warning.GetMessage());
        Assert.DoesNotContain("OrderView,", generated.Replace("OrderViewModel", ""));
    }

    [Fact]
    public void Generator_gives_the_convention_pair_one_registration_per_route_of_the_view_model()
    {
        var result = RunGenerator("""
            using Routeables.Abstraction;

            [Routeable("/cats")]
            [Routeable("/felines")]
            public class CatsViewModel { }

            public class CatsView : IRouteable { }
            """);

        var generated = GetGeneratedCode(result);

        Assert.Contains("route: \"/cats\"", generated);
        Assert.Contains("route: \"/felines\"", generated);
        Assert.Equal(2, generated.Split("RegisterRoute<global::CatsView, global::CatsViewModel>(").Length - 1);
    }

    private static GeneratorDriverRunResult RunGenerator(string source, params MetadataReference[] more)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Routeables.Generated.Tests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences().Concat(more),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new RouteRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        return driver.GetRunResult();
    }

    private static string GetGeneratedCode(GeneratorDriverRunResult runResult) =>
        string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetRoot().FullSpan.Length > 0 ? tree.ToString() : string.Empty));

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));

        return references.Concat([
            MetadataReference.CreateFromFile(typeof(RouteableAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Routeables.Routing.Extensions).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location)
        ]).DistinctBy(reference => reference.Display);
    }
}
