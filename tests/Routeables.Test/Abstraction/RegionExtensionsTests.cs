using Routeables.Abstraction.Routing;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

public class RegionExtensionsTests
{
    [Fact]
    public void AddRegionLayout_refuses_a_null_collection()
    {
        Assert.Throws<ArgumentNullException>(() => RegionExtensions.AddRegionLayout<TitleLayout>(null!, "content"));
    }

    [Fact]
    public void AddRegionLayout_returns_the_collection_for_chaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddRegionLayout<TitleLayout>("content"));
    }

    [Fact]
    public void The_layout_is_scoped_and_each_name_gets_a_router_of_its_own_in_each_scope()
    {
        var services = new ServiceCollection();
        services.AddRegionLayout<TitleLayout>("content", "header");

        Assert.Equal(ServiceLifetime.Scoped, services.Single(d => d.ServiceType == typeof(TitleLayout)).Lifetime);
        Assert.Equal(["content", "header"], services.Where(d => d.ServiceType == typeof(IRouter)).Select(d => (string)d.ServiceKey!));
    }

    [Fact]
    public void No_name_registers_the_layout_alone()
    {
        var services = new ServiceCollection();

        services.AddRegionLayout<TitleLayout>();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IRouter));
        Assert.Contains(services, d => d.ServiceType == typeof(TitleLayout));
    }

    [Fact]
    public void An_empty_region_name_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddRegionLayout<TitleLayout>(" "));
    }
}
