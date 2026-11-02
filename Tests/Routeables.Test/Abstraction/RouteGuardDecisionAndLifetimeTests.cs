using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Test.Abstraction;

/// <summary><see cref="RouteGuardDecision"/>, the lifetime the host asks for and the keys of a view.</summary>
public class RouteGuardDecisionAndLifetimeTests
{
    [Fact]
    public void Allow_goes_on_and_redirects_nowhere()
    {
        Assert.True(RouteGuardDecision.Allow.IsAllowed);
        Assert.Null(RouteGuardDecision.Allow.RedirectTo);
    }

    [Fact]
    public void Cancel_stops_and_redirects_nowhere()
    {
        Assert.False(RouteGuardDecision.Cancel.IsAllowed);
        Assert.Null(RouteGuardDecision.Cancel.RedirectTo);
    }

    [Fact]
    public void Redirect_stops_and_names_the_route()
    {
        var decision = RouteGuardDecision.Redirect("/sign-in");

        Assert.False(decision.IsAllowed);
        Assert.Equal("/sign-in", decision.RedirectTo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Redirect_refuses_a_blank_route(string? route)
        => Assert.ThrowsAny<ArgumentException>(() => RouteGuardDecision.Redirect(route!));

    [Fact]
    public void Allow_and_Cancel_are_shared_instances()
    {
        Assert.Same(RouteGuardDecision.Allow, RouteGuardDecision.Allow);
        Assert.Same(RouteGuardDecision.Cancel, RouteGuardDecision.Cancel);
        Assert.NotSame(RouteGuardDecision.Allow, RouteGuardDecision.Cancel);
    }

    [Fact]
    public void Each_redirect_is_its_own_decision()
        => Assert.NotSame(RouteGuardDecision.Redirect("/a"), RouteGuardDecision.Redirect("/a"));

    // ------------------------------------------------------------------------------------------------ the lifetime of the host

    [Fact]
    public void Without_a_host_the_lifetime_is_singleton()
        => Assert.Equal(ServiceLifetime.Singleton, new ServiceCollection().GetRouteableLifetime());

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void The_lifetime_is_the_one_the_host_registered(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RouteableHostLifetime(lifetime));

        Assert.Equal(lifetime, services.GetRouteableLifetime());
    }

    [Fact]
    public void The_last_registration_of_the_host_wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RouteableHostLifetime(ServiceLifetime.Singleton));
        services.AddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));

        Assert.Equal(ServiceLifetime.Scoped, services.GetRouteableLifetime());
    }

    [Fact]
    public void The_lifetime_refuses_no_services()
        => Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).GetRouteableLifetime());

    // ------------------------------------------------------------------------------------------------ the keys

    [Fact]
    public void Without_a_route_the_key_of_a_view_is_made_of_its_type()
    {
        var route = RouteableKeys.GetRoute(typeof(string));

        Assert.StartsWith("v:String", route);
        Assert.Contains(typeof(string).FullName!, route);
    }

    [Fact]
    public void With_a_route_the_key_is_the_route()
        => Assert.Equal("/orders", RouteableKeys.GetRoute(typeof(string), "/orders"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_route_is_the_same_as_none(string? blank)
        => Assert.Equal(RouteableKeys.GetRoute(typeof(string)), RouteableKeys.GetRoute(typeof(string), blank));

    [Fact]
    public void The_template_of_a_view_is_its_route()
    {
        Assert.Equal(RouteableKeys.GetRoute(typeof(string), "/a"), RouteableKeys.GetTemplate(typeof(string), "/a"));
        Assert.Equal(RouteableKeys.GetRoute(typeof(string)), RouteableKeys.GetTemplate(typeof(string)));
    }

    [Fact]
    public void Two_types_of_the_same_name_have_different_keys()
        => Assert.NotEqual(RouteableKeys.GetRoute(typeof(string)), RouteableKeys.GetRoute(typeof(int)));

    [Fact]
    public void A_key_with_a_type_shows_the_type_and_one_without_does_not()
    {
        Assert.Equal("k", RouteableKeys.For("k").ToString());
        Assert.Equal($"k:{typeof(int).FullName}", RouteableKeys.For("k", typeof(int)).ToString());
    }

    [Fact]
    public void Keys_of_the_same_content_are_equal()
    {
        Assert.Equal(RouteableKeys.For("k", typeof(int)), RouteableKeys.For("k", typeof(int)));
        Assert.NotEqual(RouteableKeys.For("k", typeof(int)), RouteableKeys.For("k", typeof(long)));
    }
}
