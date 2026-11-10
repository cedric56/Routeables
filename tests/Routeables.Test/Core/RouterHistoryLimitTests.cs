using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>The router on a history with a limit (<c>services.LimitHistory</c>): the oldest page is dropped and destroyed like a popped one.</summary>
public class RouterHistoryLimitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IRouter CreateRouter(int? maxSize)
        => new Router(new RouterHistory(maxSize), new RouterGate(), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());

    private sealed class Page : IRouteable, IDisposable
    {
        public bool Disposed { get; private set; }

        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task Beyond_the_limit_the_oldest_page_is_dropped_and_disposed()
    {
        var router = CreateRouter(2);
        var pages = new[] { new Page(), new Page(), new Page() };

        foreach (var page in pages)
            await router.GoAsync(new TestView(), page, cancellationToken: Ct);

        Assert.Equal(2, router.History.Count);
        Assert.Equal(2, router.History.MaxSize);
        Assert.True(pages[0].Disposed);
        Assert.False(pages[1].Disposed);
        Assert.False(pages[2].Disposed);
        Assert.Same(pages[1], router.History.Root!.ViewModel);
        Assert.Same(pages[2], router.CurrentViewModel);
    }

    [Fact]
    public async Task Without_a_limit_no_page_is_dropped()
    {
        var router = CreateRouter(null);
        var pages = Enumerable.Range(0, 30).Select(_ => new Page()).ToArray();

        foreach (var page in pages)
            await router.GoAsync(new TestView(), page, cancellationToken: Ct);

        Assert.Equal(30, router.History.Count);
        Assert.Null(router.History.MaxSize);
        Assert.All(pages, page => Assert.False(page.Disposed));
    }

    [Fact]
    public async Task A_page_left_with_back_stays_until_a_push_needs_the_room()
    {
        var router = CreateRouter(2);
        var first = new Page();
        var second = new Page();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);
        await router.GoAsync(new TestView(), second, cancellationToken: Ct);

        await router.GoBackAsync(Ct);

        Assert.Equal(2, router.History.Count);
        Assert.False(first.Disposed);
        Assert.False(second.Disposed);
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped, 50)]
    [InlineData(ServiceLifetime.Singleton, null)]
    public void A_scoped_router_keeps_50_entries_unless_a_limit_is_set(ServiceLifetime lifetime, int? expected)
    {
        var services = new ServiceCollection();
        services.RegisterRouting(lifetime);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal(expected, scope.ServiceProvider.GetRequiredService<RouterHistory>().MaxSize);
    }

    [Fact]
    public void A_limit_replaces_the_default_of_a_scoped_router()
    {
        var services = new ServiceCollection();
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.LimitHistory(7);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal(7, scope.ServiceProvider.GetRequiredService<RouterHistory>().MaxSize);
    }
}
