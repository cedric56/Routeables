using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>The contract of every host (<see cref="Routeables.Test.HostContract"/>) on the bare container, the reference the hosts are compared to.</summary>
public class HostContractTests
{
    public sealed class PageA : IRouteableFor { public object? Context { get; set; } }
    public sealed class PageAVm { }
    public sealed class PageB : IRouteableFor { public object? Context { get; set; } }
    public sealed class PageBVm { }

    [Fact]
    public async Task The_bare_container_honours_the_host_contract()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        services.RegisterRoute<PageA, PageAVm>(route: "/a");
        services.RegisterRoute<PageB, PageBVm>(route: "/b");

        await Routeables.Test.HostContract.RunAsync(services.BuildServiceProvider());
    }
}
