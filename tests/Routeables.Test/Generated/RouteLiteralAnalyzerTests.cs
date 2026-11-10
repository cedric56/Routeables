using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Routeables.Abstraction.Routing;
using Routeables.Generated;

namespace Routeables.Tests.Generated;

public class RouteLiteralAnalyzerTests
{
    private const string Declarations = """
        using System;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Routeables.Abstraction;
        using Routeables.Abstraction.Routing;
        using Routeables.Abstraction.Registrations;
        [Routeable("/monkeys")] public class MonkeysViewModel { }
        [Routeable("/monkeys/{name}")] public class MonkeyDetailViewModel { }
        [Routeable("/files/{**path}")] public class FilesViewModel { }
        [Routeable("/orders/{id:int}/{tab?}")] public class OrdersViewModel { }
        public class Entry { public string Route => "/about"; }
        """;

    private static async Task<string[]> Analyze(string statement, string extra = "")
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
            "Routeables.Analyzer.Tests",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var diagnostics = await compilation.WithAnalyzers([new RouteLiteralAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return diagnostics.Select(d => d.Id).ToArray();
    }

    [Fact]
    public async Task A_misspelled_route_is_reported()
        => Assert.Equal(["ROUT501"], await Analyze("""await router.GoToAsync("/monkies");"""));

    [Fact]
    public async Task A_route_that_matches_a_registered_one_is_not_reported()
    {
        foreach (var route in new[] { "/monkeys", "monkeys", "/MONKEYS/", "/monkeys/Rex", "/monkeys?x=1", "/files/a/b/c", "/orders/4", "/orders/4/edit", "/about", "-", "-/monkeys", "https://example.com/x" })
            Assert.Empty(await Analyze($$"""await router.GoToAsync("{{route}}");"""));
    }

    [Fact]
    public async Task Too_many_or_too_few_segments_are_reported()
    {
        foreach (var route in new[] { "/monkeys/Rex/extra", "/orders", "/orders/4/edit/more", "/-/nope" })
            Assert.Equal(["ROUT501"], await Analyze($$"""await router.GoToAsync("{{route}}");"""));
    }

    [Fact]
    public async Task The_provider_overload_is_checked_too()
        => Assert.Equal(["ROUT501"], await Analyze("""await provider.GoToAsync("/nope");"""));

    [Fact]
    public async Task A_call_with_a_template_or_a_variable_is_left_alone()
    {
        Assert.Empty(await Analyze("""await router.GoToAsync("/anything", null, "/monkeys");"""));
        Assert.Empty(await Analyze("""var r = "/x" + DateTime.Now; await router.GoToAsync(r);"""));
    }

    [Fact]
    public async Task A_route_registered_with_RegisterRoute_is_known()
    {
        Assert.Empty(await Analyze("""services.RegisterRoute("/dogs/{id}", (sp, a) => new object()); await router.GoToAsync("/dogs/3");"""));
        // a route only known at run time: nothing can be said
        Assert.Empty(await Analyze("""var p = Environment.GetEnvironmentVariable("X")!; services.RegisterRoute(p, (sp, a) => new object()); await router.GoToAsync("/whatever");"""));
    }

    [Fact]
    public async Task Without_any_known_route_nothing_is_reported()
    {
        var compilation = CSharpCompilation.Create(
            "NoRoutes",
            [CSharpSyntaxTree.ParseText("""
                using Routeables.Abstraction.Routing;
                public static class C { public static void M(IRouter r) => r.GoToAsync("/x"); }
                """)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new RouteLiteralAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics);
    }

    private static IEnumerable<MetadataReference> References()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            // The framework only: the test project and the samples it loads carry [Routeable] types of their own, which would be known routes.
            .Where(path => Path.GetFileName(path) is { } name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "mscorlib.dll" or "netstandard.dll"))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Concat([
                MetadataReference.CreateFromFile(typeof(IRouter).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location)])
            .DistinctBy(r => r.Display);
}
