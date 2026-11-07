using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>The catch-all placeholder <c>{**path}</c>: it takes what is left of the path, slashes included.</summary>
public class RouteCatchAllTests
{
    public sealed class DocsView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DocsVm(string? path = null) { public string? Path { get; } = path; }

    public sealed class IntroView : IRouteableFor { public object? Context { get; set; } }
    public sealed class IntroVm { }

    private static IRouter Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<IRouter>();
    }

    [Fact]
    public async Task The_catch_all_takes_the_rest_of_the_path_with_its_slashes()
    {
        var router = Build(s => s.RegisterRoute<DocsView, DocsVm>(route: "/docs/{**path}"));

        var result = await router.GoToAsync("/docs/guides/a%20b/c", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("guides/a b/c", Assert.IsType<DocsVm>(router.CurrentViewModel).Path);
    }

    [Fact]
    public async Task The_catch_all_may_be_empty()
    {
        var router = Build(s => s.RegisterRoute<DocsView, DocsVm>(route: "/docs/{**path}"));

        var result = await router.GoToAsync("/docs", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(Assert.IsType<DocsVm>(router.CurrentViewModel).Path);
    }

    [Fact]
    public async Task A_literal_route_wins_over_the_catch_all()
    {
        var router = Build(s =>
        {
            s.RegisterRoute<DocsView, DocsVm>(route: "/docs/{**path}");
            s.RegisterRoute<IntroView, IntroVm>(route: "/docs/intro");
        });

        await router.GoToAsync("/docs/intro", cancellationToken: TestContext.Current.CancellationToken);
        Assert.IsType<IntroVm>(router.CurrentViewModel);

        await router.GoToAsync("/docs/other/page", cancellationToken: TestContext.Current.CancellationToken);
        Assert.IsType<DocsVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task The_catch_all_is_filled_when_the_route_is_built()
    {
        var router = Build(s => s.RegisterRoute<DocsView, DocsVm>(route: "/docs/{**path}"));

        var result = await router.GoToAsync<DocsVm>(
            new Dictionary<string, object?> { ["path"] = "guides/a b/c" }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("/docs/guides/a%20b/c", router.History.Current!.Args.Route);
    }

    [Fact]
    public async Task A_catch_all_that_is_not_the_last_segment_never_matches()
    {
        var router = Build(s => s.RegisterRoute<DocsView, DocsVm>(route: "/docs/{**path}/edit"));

        var result = await router.GoToAsync("/docs/a/edit", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }
}
