using Avalonia.Threading;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The contract every host honours (see <see cref="Routeables.Test.RouterContract"/>), run on the Avalonia container.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessRouterContractTests(HeadlessApp app)
{
    /// <summary>Back, Forward, Pop, refusals and Routing/Routed pairing behave as they do on every other host.</summary>
    [Fact]
    public Task Avalonia_routes_by_the_router_contract() => app.RunAsync(() =>
        Routeables.Test.RouterContract.RunAsync(app.Provider, () =>
        {
            Dispatcher.UIThread.RunJobs();
            return Task.CompletedTask;
        }));
}
