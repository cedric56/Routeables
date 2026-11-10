using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Generated;
using Routeables.Abstraction.Registrations;
namespace Routeables.Tests.Generated;

/// <summary>The generated routes reach the host without <c>AddGeneratedRoutes()</c>: the module initializer of an executable, and the merge the host does.</summary>
public class RouteAutoRegistrationTests
{
    public sealed class AboutViewModel;
    public sealed class AboutView;
    public sealed class HelpViewModel;
    public sealed class HelpView;

    private const string Source = """
        using Routeables.Abstraction;

        public static class Program { public static void Main() { } }

        [Routeable("/home")]
        public class HomeViewModel { }

        [RouteableView(typeof(HomeViewModel))]
        public class HomeView { }
        """;

    private static string Generate(OutputKind kind, string? autoRegister = null)
    {
        var compilation = CSharpCompilation.Create(
            "Routeables.Generated.AutoTests",
            [CSharpSyntaxTree.ParseText(Source)],
            References(),
            new CSharpCompilationOptions(kind));
        var options = new Options(autoRegister);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new RouteRegistrationGenerator().AsSourceGenerator()], optionsProvider: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList(); Assert.True(errors.Count == 0, string.Join(" | ", errors.Take(4)));
        return string.Join(Environment.NewLine, driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));
    }

    private sealed class Options(string? value) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Global(value);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class Global(string? value) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value1)
            {
                value1 = value!;
                return value is not null && key == "build_property.RouteablesAutoRegister";
            }
        }
    }

    private static IEnumerable<MetadataReference> References() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
        .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
        .Concat([
            MetadataReference.CreateFromFile(typeof(RouteableAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Routeables.Routing.Extensions).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceCollectionContainerBuilderExtensions).Assembly.Location),
        ])
        .DistinctBy(r => r.Display);

    [Fact]
    public void An_executable_hands_its_routes_to_the_host()
    {
        foreach (var kind in new[] { OutputKind.ConsoleApplication, OutputKind.WindowsApplication })
        {
            var generated = Generate(kind);

            Assert.Contains("ModuleInitializer", generated);
            Assert.Contains("RouteableAutoRegistration.Add(", generated);
        }
    }

    [Fact]
    public void A_library_does_not_register_itself()
    {
        var generated = Generate(OutputKind.DynamicallyLinkedLibrary);

        Assert.DoesNotContain("ModuleInitializer", generated);
        Assert.Contains("AddGeneratedRoutes", generated);
    }

    [Fact]
    public void The_property_switches_it_off_in_an_executable()
    {
        Assert.DoesNotContain("ModuleInitializer", Generate(OutputKind.ConsoleApplication, autoRegister: "false"));
        Assert.Contains("ModuleInitializer", Generate(OutputKind.ConsoleApplication, autoRegister: "true"));
        // "true" also asks for it where the compiler does not see an executable.
        Assert.Contains("ModuleInitializer", Generate(OutputKind.DynamicallyLinkedLibrary, autoRegister: "true"));
    }

    [Fact]
    public void AddGeneratedRoutes_marks_the_collection_so_that_the_host_does_not_add_them_twice()
    {
        Assert.Contains("RouteableAutoRegistration.Mark(services, typeof(RouteRegistration).Assembly)", Generate(OutputKind.DynamicallyLinkedLibrary));
    }

    // ---- what the host does with them

    private static Action<IServiceCollection> Generated(params Action<IServiceCollection>[] pages) => services =>
    {
        RouteableAutoRegistration.Mark(services);
        foreach (var page in pages)
            page(services);
    };

    private static readonly Action<IServiceCollection> About = s => s.RegisterRoute<AboutView, AboutViewModel>(route: "/about");
    private static readonly Action<IServiceCollection> Help = s => s.RegisterRoute<HelpView, HelpViewModel>(route: "/help");

    private static Type? Open(IServiceCollection services, string route)
        => services.GetRouteableTable().Entries.FirstOrDefault(e => e.Template == route).ViewModelType;

    private static bool IsRegistered(IServiceCollection services, string key)
        => services.Any(d => d.IsKeyedService && Equals(d.ServiceKey, key));

    [Fact]
    public void The_host_adds_the_generated_routes_the_application_did_not_register()
    {
        var services = new ServiceCollection();
        services.RegisterRoute<AboutView, AboutViewModel>(route: "/about");

        var applied = RouteableAutoRegistration.Apply(services,
            [Generated(About, Help)]);

        Assert.True(applied);
        Assert.Equal(typeof(AboutViewModel), Open(services, "/about"));
        Assert.Equal(typeof(HelpViewModel), Open(services, "/help"));
    }

    [Fact]
    public void A_route_the_application_registered_is_not_registered_a_second_time()
    {
        var services = new ServiceCollection();
        var explicitFactory = new Func<IServiceProvider, IRouteableArgs, object>((_, _) => "mine");
        services.AddKeyedSingleton("/about", explicitFactory);
        var before = services.Count(d => d.IsKeyedService && Equals(d.ServiceKey, "/about"));

        RouteableAutoRegistration.Apply(services, [Generated(About)]);

        // The key "/about" of the view is the application's own: nothing was added under it.
        Assert.Equal(before, services.Count(d => d.IsKeyedService && Equals(d.ServiceKey, "/about") && d.KeyedImplementationInstance is Func<IServiceProvider, IRouteableArgs, object>));
        Assert.Same(explicitFactory, services.Last(d => d.IsKeyedService && Equals(d.ServiceKey, "/about")).KeyedImplementationInstance);
    }

    [Fact]
    public void Nothing_is_added_when_the_application_called_AddGeneratedRoutes()
    {
        var services = new ServiceCollection();
        RouteableAutoRegistration.Mark(services);

        var applied = RouteableAutoRegistration.Apply(services, [Generated(Help)]);

        Assert.False(applied);
        Assert.False(IsRegistered(services, "/help"));
    }

    [Fact]
    public void Applying_twice_adds_the_routes_once()
    {
        var services = new ServiceCollection();
        var generated = Generated(Help);

        Assert.True(RouteableAutoRegistration.Apply(services, [generated]));
        var count = services.Count;
        Assert.False(RouteableAutoRegistration.Apply(services, [generated]));

        Assert.Equal(count, services.Count);
    }

    [Fact]
    public void Without_any_registration_nothing_happens()
    {
        var services = new ServiceCollection();

        Assert.False(RouteableAutoRegistration.Apply(services, Array.Empty<Action<IServiceCollection>>()));
        Assert.Empty(services);
    }

    // ---- a plain view found by its name

    private static string GenerateFrom(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Routeables.Generated.ConventionTests",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new RouteRegistrationGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();
        Assert.True(errors.Count == 0, string.Join(" | ", errors.Take(4)));
        LastDiagnostics = [.. diagnostics.Select(d => d.Id)];
        return string.Join(Environment.NewLine, driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));
    }

    private static string[] LastDiagnostics = [];

    [Fact]
    public void A_view_that_implements_nothing_is_found_by_its_name()
    {
        var generated = GenerateFrom("""
            using Routeables.Abstraction;
            using Routeables.Abstraction.Registrations;
            [Routeable("/dogs")]
            public class DogsViewModel { }

            public class DogsView { }
            """);

        Assert.Contains("RegisterRoute<global::DogsView, global::DogsViewModel>", generated);
        Assert.Contains("route: \"/dogs\"", generated);
    }

    [Fact]
    public void A_class_named_like_a_view_is_left_alone_when_no_view_model_asks_for_it()
    {
        var generated = GenerateFrom("""
            using Routeables.Abstraction;

            [Routeable("/dogs")]
            public class DogsViewModel { }

            public class CatsView { }
            """);

        Assert.DoesNotContain("CatsView", generated);
    }

    [Fact]
    public void An_abstract_or_generic_class_is_not_taken_for_the_view()
    {
        var generated = GenerateFrom("""
            using Routeables.Abstraction;
            using Routeables.Abstraction.Registrations;
            [Routeable("/dogs")]
            public class DogsViewModel { }

            public abstract class DogsView { }
            """);

        Assert.DoesNotContain("RegisterRoute<global::DogsView", generated);
    }

    [Fact]
    public void Two_views_with_the_same_name_are_reported_as_before()
    {
        GenerateFrom("""
            using Routeables.Abstraction;

            namespace A { [Routeable("/dogs")] public class DogsViewModel { } }
            namespace B { public class DogsView { } }
            namespace C { public class DogsView { } }
            """);

        Assert.Contains("ROUT202", LastDiagnostics);
    }

    // ---- a library's AddGeneratedRoutes() must not hide the routes of the application

    private static Action<IServiceCollection> GeneratedBy(System.Reflection.Assembly owner, params Action<IServiceCollection>[] pages) => services =>
    {
        RouteableAutoRegistration.Mark(services, owner);
        foreach (var page in pages)
            page(services);
    };

    private static readonly System.Reflection.Assembly Library = typeof(object).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(RouteAutoRegistrationTests).Assembly;

    [Fact]
    public void The_generated_routes_of_a_library_do_not_hide_those_of_the_application()
    {
        var services = new ServiceCollection();
        GeneratedBy(Library, About)(services);   // AddBirdsModule(): the library's own AddGeneratedRoutes()

        var applied = RouteableAutoRegistration.Apply(services, [(Application, GeneratedBy(Application, About, Help))]);

        Assert.True(applied);
        Assert.Equal(typeof(HelpViewModel), Open(services, "/help"));
    }

    [Fact]
    public void The_application_that_called_its_own_AddGeneratedRoutes_is_not_applied_twice()
    {
        var services = new ServiceCollection();
        GeneratedBy(Application, About)(services);

        Assert.False(RouteableAutoRegistration.Apply(services, [(Application, GeneratedBy(Application, About, Help))]));
        Assert.Null(Open(services, "/help"));
    }

    [Fact]
    public void The_marks_of_two_assemblies_are_both_kept()
    {
        var services = new ServiceCollection();
        GeneratedBy(Library, About)(services);
        RouteableAutoRegistration.Apply(services, [(Application, GeneratedBy(Application, Help))]);

        Assert.False(RouteableAutoRegistration.Apply(services, [(Library, GeneratedBy(Library, About))]));
        Assert.False(RouteableAutoRegistration.Apply(services, [(Application, GeneratedBy(Application, Help))]));
    }
}
