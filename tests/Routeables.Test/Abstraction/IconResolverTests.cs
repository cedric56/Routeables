using NSubstitute;
using Routeables.Abstraction;

namespace Routeables.Test.Abstraction;

/// <summary>
/// <see cref="RouteableIconResolver"/> holds a single, process-wide, append-only list of providers with no way
/// to unregister them, so every test here uses a GUID-based key that is unique to that test. This keeps
/// tests independent of each other and safe to run in parallel, even though they all share the same
/// static registry.
/// </summary>
public class IconResolverTests
{
    [Fact]
    public void GetIcon_and_GetSource_return_null_when_no_provider_resolves_the_key()
    {
        var key = $"unknown-{Guid.NewGuid()}";

        Assert.Null(RouteableIconResolver.GetIcon(key));
        Assert.Null(RouteableIconResolver.GetSource(key));
    }

    [Fact]
    public void GetIcon_returns_the_value_from_a_registered_provider()
    {
        var key = $"icon-{Guid.NewGuid()}";
        var icon = new object();
        var provider = Substitute.For<IRouteableIconResolver>();
        provider.GetIcon(key).Returns(icon);

        RouteableIconResolver.Register(provider);

        Assert.Same(icon, RouteableIconResolver.GetIcon(key));
    }

    [Fact]
    public void GetSource_returns_the_value_from_a_registered_provider()
    {
        var key = $"source-{Guid.NewGuid()}";
        var source = new object();
        var provider = Substitute.For<IRouteableIconResolver>();
        provider.GetSource(key).Returns(source);

        RouteableIconResolver.Register(provider);

        Assert.Same(source, RouteableIconResolver.GetSource(key));
    }

    [Fact]
    public void GetIcon_falls_through_to_the_next_provider_when_the_first_returns_null()
    {
        var key = $"fallback-{Guid.NewGuid()}";
        var icon = new object();

        var first = Substitute.For<IRouteableIconResolver>();
        first.GetIcon(key).Returns((object?)null);

        var second = Substitute.For<IRouteableIconResolver>();
        second.GetIcon(key).Returns(icon);

        RouteableIconResolver.Register(first);
        RouteableIconResolver.Register(second);

        Assert.Same(icon, RouteableIconResolver.GetIcon(key));
    }
}
