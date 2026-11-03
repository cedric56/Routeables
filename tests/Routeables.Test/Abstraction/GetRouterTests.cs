using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

/// <summary>Asking for a region that was never registered says which region, and how to register it.</summary>
public class GetRouterTests
{
    [Fact]
    public void An_unknown_region_names_itself_and_the_way_to_register_it()
    {
        using var provider = RegionTestServices.Build(services => services.AddRegion("content"));

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRouter("sidebar"));

        Assert.Contains("'sidebar'", error.Message);
        Assert.Contains("AddRegion", error.Message);
    }

    [Fact]
    public void A_registered_region_and_the_default_router_are_found()
    {
        using var provider = RegionTestServices.Build(services => services.AddRegion("content"));

        Assert.NotNull(provider.GetRouter("content"));
        Assert.Same(provider.GetRouter(null), provider.GetRouter(" "));
    }

    [Fact]
    public async Task The_region_of_the_arguments_chooses_the_router_of_provider_GoToAsync()
    {
        using var provider = RegionTestServices.Build(services => services.AddRegion("content"));
        var args = Routeables.Routing.RouteableArgs.Create().WithRegion("sidebar");

        Assert.Equal("sidebar", args.Region);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GoToAsync("/anything", args, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("'sidebar'", error.Message);
    }
}
