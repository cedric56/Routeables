using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary>The contract every host honours (see <see cref="Routeables.Test.RouterContract"/>), run on the router of the Uno application.</summary>
[TestClass]
[RunsOnUIThread]
public class RouterContractTests
{
    /// <summary>Back, Forward, Pop, refusals and the Routing/Routed pairing behave as they do on every other host.</summary>
    [TestMethod]
    public async Task Uno_routes_by_the_router_contract()
    {
        await TestHost.ResetAsync();
        await Routeables.Test.RouterContract.RunAsync(TestHost.Services, () => Task.CompletedTask);
        await TestHost.ResetAsync();
    }
}
