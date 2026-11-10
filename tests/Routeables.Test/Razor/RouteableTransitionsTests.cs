using Routeables.Abstraction;
using Routeables.Abstraction.Transitions;
using Routeables.AspNetCore;
using Routeables.Routing;

namespace Routeables.Tests.Razor;

/// <summary>The transition of a region on a host that sends whole pages: <see cref="RouteableTransitions"/>.</summary>
public class RouteableTransitionsTests
{
    public sealed class FirstView : IRouteableFor { public object? Context { get; set; } }
    public sealed class FirstVm;
    public sealed class SecondView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SecondVm;

    private static ServiceProvider Application()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterRouting();
        services.AddRegion("side");
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        services.RegisterRoute<FirstView, FirstVm>(route: "/first");
        services.RegisterRoute<SecondView, SecondVm>(route: "/second");
        services.AddScoped<RouteableTransitions>();
        return services.BuildServiceProvider();
    }

    private static Task<RouteableResult> Go(IServiceProvider provider, string route, IRouteableArgs? args = null, string? region = null)
        => provider.GetRouter(region).GoToAsync(route, args, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_first_page_of_a_region_comes_in_without_a_transition()
    {
        await using var provider = Application();
        await Go(provider, "/first");
        using var transitions = new RouteableTransitions(provider);

        var style = transitions.Next(regionTransition: RouteTransition.Slide);

        Assert.False(style.Animated);
        Assert.Equal(string.Empty, style.CssClass);
    }

    [Fact]
    public async Task A_page_that_follows_a_route_change_gets_the_transition_of_the_region()
    {
        await using var provider = Application();
        using var transitions = new RouteableTransitions(provider);
        await Go(provider, "/first");
        transitions.Next();                                  // the region is followed from here on

        await Go(provider, "/second");
        var style = transitions.Next(regionTransition: RouteTransition.Slide);

        Assert.True(style.Animated);
        Assert.Equal("rt-enter rt-slide rt-forward", style.CssClass);
        Assert.Equal("--rt-duration:250ms", style.Style);
    }

    [Fact]
    public async Task The_transition_of_the_navigation_wins_over_the_one_of_the_region()
    {
        await using var provider = Application();
        using var transitions = new RouteableTransitions(provider);
        await Go(provider, "/first");
        transitions.Next();

        await Go(provider, "/second", RouteableArgs.Create().WithTransition(RouteTransition.Fade.WithDuration(TimeSpan.FromMilliseconds(400))));
        var style = transitions.Next(regionTransition: RouteTransition.Slide);

        Assert.Equal("rt-enter rt-fade rt-forward", style.CssClass);
        Assert.Equal("--rt-duration:400ms", style.Style);
    }

    [Fact]
    public async Task A_transition_plays_once_a_reload_plays_none()
    {
        await using var provider = Application();
        using var transitions = new RouteableTransitions(provider);
        await Go(provider, "/first");
        transitions.Next();
        await Go(provider, "/second");

        Assert.True(transitions.Next(regionTransition: RouteTransition.Fade).Animated);
        Assert.False(transitions.Next(regionTransition: RouteTransition.Fade).Animated);
    }

    [Fact]
    public async Task Back_goes_the_other_way()
    {
        await using var provider = Application();
        using var transitions = new RouteableTransitions(provider);
        await Go(provider, "/first");
        await Go(provider, "/second");
        transitions.Next();

        await provider.GetRouter(null).GoBackAsync(TestContext.Current.CancellationToken);
        var style = transitions.Next(regionTransition: RouteTransition.Slide);

        Assert.Equal("rt-enter rt-slide rt-back", style.CssClass);
    }

    [Fact]
    public async Task Without_a_transition_nothing_plays_and_a_custom_name_becomes_a_class()
    {
        await using var provider = Application();
        using var transitions = new RouteableTransitions(provider);
        await Go(provider, "/first");
        transitions.Next();
        await Go(provider, "/second");
        Assert.False(transitions.Next().Animated);

        await Go(provider, "/first");
        var custom = transitions.Next(regionTransition: RouteTransition.Named("Spin 3D"));
        Assert.Equal("rt-enter rt-spin-3d rt-forward", custom.CssClass);
    }

    [Fact]
    public async Task Each_region_is_followed_on_its_own()
    {
        await using var provider = Application();
        using var transitions = new RouteableTransitions(provider);
        await Go(provider, "/first", region: "side");
        transitions.Next("side");
        transitions.Next();

        await Go(provider, "/second", region: "side");

        Assert.False(transitions.Next(regionTransition: RouteTransition.Fade).Animated);          // the default router did not move
        Assert.True(transitions.Next("side", RouteTransition.Fade).Animated);
    }

    [Fact]
    public void The_stylesheet_holds_the_animations_of_the_built_in_names()
    {
        var css = RouteableTransitions.Css;

        Assert.Contains(".rt-enter", css);
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
            Assert.Contains("rt-" + transition.Name.ToLowerInvariant(), css);
        Assert.Contains("prefers-reduced-motion", css);
    }
}
