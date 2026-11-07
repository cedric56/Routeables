using Xunit;

namespace Routeables.Test.Maui;

/// <summary>The contract every host honours (see <see cref="Routeables.Test.RouterContract"/>), run on the router of the MAUI application.</summary>
public class RouterContractDeviceTests
{
    /// <summary>Back, Forward, Pop, refusals and the Routing/Routed pairing behave as they do on every other host.</summary>
    [UIFact]
    public async Task Maui_routes_by_the_router_contract()
    {
        await Device.ResetAsync();
        await Routeables.Test.RouterContract.RunAsync(Device.Services);
        await Device.ResetAsync();
    }
}
