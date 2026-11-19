using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Tests.Pages;
using ListView = Routeables.Tests.Pages.ListView;

namespace Routeables.Tests.Core;

/// <summary>
/// What the router looks like after a routing fails or is cancelled halfway, and under concurrent calls:
/// the state stays the one of the page that is displayed, and the gate is released so the next routing works.
/// </summary>
public class RouterFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IRouter CreateRouter()
        => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());

    private sealed class FailingTarget(bool inInitialize) : IRouteable, IInitializeAsync
    {
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
            => inInitialize ? Task.CompletedTask : throw new InvalidOperationException("to failed");

        public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
            => inInitialize ? throw new InvalidOperationException("initialize failed") : Task.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_target_that_fails_while_arriving_leaves_the_router_usable(bool inInitialize)
    {
        var router = CreateRouter();
        var first = new TestViewModel();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new TestView(), new FailingTarget(inInitialize), cancellationToken: Ct));

        // The gate was released: another routing completes instead of waiting forever.
        var next = new TestViewModel();
        var result = await router.GoAsync(new TestView(), next, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.True(result.Success);
        Assert.Same(next, router.CurrentViewModel);
    }

    [Fact]
    public async Task A_routing_cancelled_before_it_starts_does_not_change_the_current_page()
    {
        var router = CreateRouter();
        var first = new TestViewModel();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        try { await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: cts.Token); }
        catch (OperationCanceledException) { }

        Assert.Same(first, router.CurrentViewModel);
        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RouteChanges_started_together_run_one_after_the_other()
    {
        var router = CreateRouter();
        var viewModels = Enumerable.Range(0, 50).Select(_ => new TestViewModel()).ToArray();

        var results = await Task.WhenAll(viewModels
            .Select(vm => Task.Run(() => router.GoAsync(new TestView(), vm, cancellationToken: Ct), Ct)))
            .WaitAsync(TimeSpan.FromSeconds(20), Ct);

        Assert.True(results.All(r => r.Success));
        Assert.Equal(50, router.History.Count);
        Assert.Contains(router.CurrentViewModel, viewModels);

        // Each page left exactly once, in a sequence: 49 of them were left for the next one.
        Assert.Equal(49, viewModels.Count(vm => vm.Events.Contains("From")));
    }
    /// <summary>Starts a route change without waiting for it, from the callback of a page, like <c>Task.Run</c> or <c>FireAndForget</c> do.</summary>
    private sealed class BackgroundNavigator(IRouter router, TaskCompletionSource<RouteableResult> done) : IRouteable
    {
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

        public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            // The copy of the execution context taken here carries what the router knew of this route change, for as long as the work lives.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(100, ct);
                    done.SetResult(await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: ct));
                }
                catch (Exception ex) { done.SetException(ex); }
            }, ct);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_route_change_started_by_work_that_was_not_awaited_is_not_called_reentrant()
    {
        var router = CreateRouter();
        var done = new TaskCompletionSource<RouteableResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        await router.GoAsync(new TestView(), new BackgroundNavigator(router, done), cancellationToken: Ct);
        var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        // The first route change was over when the work routed: it is a route change like any other.
        Assert.True(result.Success);
    }

    // ---- a view that cannot be built

    public sealed class DisposableVm : IDisposable
    {
        public static int Created;
        public static int Disposed;
        public DisposableVm() => Interlocked.Increment(ref Created);
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    [Fact]
    public async Task A_view_model_whose_view_cannot_be_built_is_disposed()
    {
        DisposableVm.Created = DisposableVm.Disposed = 0;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<UnbuildableView, DisposableVm>(
            (_, _) => throw new InvalidOperationException("the view cannot be built"), (_, _) => new DisposableVm(),
            ServiceLifetime.Transient, ServiceLifetime.Transient, "/broken");
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        var failed = false;
        try
        {
            var result = await router.GoToAsync("/broken", cancellationToken: Ct);
            failed = !result.Success;
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }

        Assert.True(failed);
        Assert.Equal(1, DisposableVm.Created);
        Assert.Equal(1, DisposableVm.Disposed);
    }

    // ---- a go back then to a route that does not open

    public sealed class StartView : IRouteableFor { public object? Context { get; set; } }
    public sealed class ThrowingVm { public ThrowingVm() => throw new InvalidOperationException("no"); }

    [Fact]
    public async Task A_go_back_then_to_route_that_does_not_open_shows_the_page_that_was_left()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<StartView, StartVm>(route: "/home");
        services.RegisterRoute<ListView, ListVm>(route: "/orders");
        services.RegisterRoute<ThrowingView, ThrowingVm>(route: "/broken");
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        await router.GoToAsync("/orders", cancellationToken: Ct);

        RouteableResult? result = null;
        try { result = await router.GoToAsync("-/broken", cancellationToken: Ct); }
        catch (InvalidOperationException) { }

        Assert.True(result is null || !result.Success);
        Assert.IsType<ListVm>(router.CurrentViewModel);
    }
}
