using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public class IconRegistryIsolationTests
{
    private sealed class Fixed(string key, object icon) : IIconResolver
    {
        public object? GetIcon(string requested) => requested == key ? icon : null;
        public object? GetSource(string requested) => requested == key ? icon : null;
    }

    [Fact]
    public void A_view_with_its_own_registry_does_not_see_the_process_wide_icons()
    {
        var key = $"own-{Guid.NewGuid()}";
        var global = new object();
        using var token = IconResolver.Register(new Fixed(key, global));

        var own = new IconResolverRegistry();
        IRouteableEntry view = new RouteableEntry("/a", "/a", "A", key) { Icons = own };

        Assert.Null(view.Icon);                                 // its registry is empty
        var local = new object();
        own.Register(new Fixed(key, local));
        Assert.Same(local, view.Icon);
        Assert.Same(local, view.Source);
    }

    [Fact]
    public void A_view_without_a_registry_uses_the_process_wide_one()
    {
        var key = $"shared-{Guid.NewGuid()}";
        var icon = new object();
        using var token = IconResolver.Register(new Fixed(key, icon));

        IRouteableEntry view = new RouteableEntry("/a", "/a", "A", key);

        Assert.Same(icon, view.Icon);
        Assert.Null(view.Icons);
    }

    [Fact]
    public void The_own_registry_does_not_leak_into_the_process_wide_one()
    {
        var key = $"leak-{Guid.NewGuid()}";
        new IconResolverRegistry().Register(new Fixed(key, new object()));

        Assert.Null(IconResolver.GetIcon(key));
    }
}
