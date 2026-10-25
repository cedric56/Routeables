using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>A page built for a route change that did not show it (a guard refused) is disposed: nobody else would.</summary>
public class RouterDiscardTests
{
    public sealed class DiscardView : IRouteableFor, IDisposable
    {
        public object? Context { get; set; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    public sealed class DiscardVm : IDisposable
    {
        public static readonly List<DiscardVm> All = [];
        public bool Disposed { get; private set; }
        public DiscardVm() { lock (All) All.Add(this); }
        public void Dispose() => Disposed = true;
    }

    private sealed class RefuseAll : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(RouteGuardDecision.Cancel);
    }

    [Fact]
    public async Task A_refused_page_is_disposed_and_a_shown_one_is_not()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<DiscardView, DiscardVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/discard");
        services.AddTransient<DiscardVm>();
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        DiscardVm.All.Clear();

        await router.GoToAsync("/discard", cancellationToken: ct);
        var shown = (DiscardVm)router.CurrentViewModel!;
        Assert.False(shown.Disposed);

        var guarded = new ServiceCollection();
        guarded.RegisterRouting();
        guarded.RegisterRoute<DiscardView, DiscardVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/discard");
        guarded.AddTransient<DiscardVm>();
        guarded.AddSingleton<IRouteGuard, RefuseAll>();
        using var guardedProvider = guarded.BuildServiceProvider();
        DiscardVm.All.Clear();

        var result = await guardedProvider.GetRequiredService<IRouter>().GoToAsync("/discard", cancellationToken: ct);

        Assert.True(result.Cancelled);
        Assert.Single(DiscardVm.All);
        Assert.True(DiscardVm.All[0].Disposed);
    }
}
