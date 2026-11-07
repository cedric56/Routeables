using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>What a route change costs must not depend on how long the history is: the history of an application that runs for days is long.</summary>
public class RouterCostTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Page;

    private static async Task<long> BytesPerRouteChangeAsync(IRouter router, int count)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++)
            await router.GoAsync(new TestView(), new Page(), cancellationToken: Ct);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / count;
    }

    [Fact]
    public async Task A_route_change_allocates_the_same_on_a_long_history_as_on_a_short_one()
    {
        var router = new Router(new RouterHistory(null), new RouterGate(), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());
        await BytesPerRouteChangeAsync(router, 50);                   // JIT and caches
        var shortHistory = await BytesPerRouteChangeAsync(router, 100);

        await BytesPerRouteChangeAsync(router, 3_000);
        var longHistory = await BytesPerRouteChangeAsync(router, 100);

        // A copy of the history for each route change would make the second number several times the first.
        Assert.True(router.History.Count > 3_000);
        Assert.True(longHistory < shortHistory * 1.5 + 512, $"{longHistory} bytes on {router.History.Count} entries, {shortHistory} on 150");
    }
}
