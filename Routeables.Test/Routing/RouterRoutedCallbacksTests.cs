using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary><see cref="IRouteable.OnLeftAsync"/> / <see cref="IRouteable.OnEnteredAsync"/> through a real <see cref="Router"/>.</summary>
public class RouterRoutedCallbacksTests
{
    private static readonly List<string> Log = [];

    public sealed class PageView : IRouteableFor, IRouteable
    {
        public object? Context { get; set; }
        public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("PageView:routedTo"); return Task.CompletedTask; }
        public Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("PageView:routedFrom"); return Task.CompletedTask; }
    }

    public sealed class PageVm : IRouteable, IDisposable
    {
        public IRouter? Router { get; set; }
        public int HistoryCountWhenRoutedTo { get; private set; } = -1;
        public bool DisposedWhenRoutedFrom { get; private set; }
        public bool Disposed { get; private set; }
        public static bool ThrowOnEntered;

        public async Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            Log.Add("PageVm:routedTo");
            if (ThrowOnEntered) throw new InvalidOperationException("boom");
            await Task.CompletedTask;
        }

        public Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            Log.Add("PageVm:routedFrom");
            DisposedWhenRoutedFrom = Disposed;
            return Task.CompletedTask;
        }

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("PageVm:routingTo"); return Task.CompletedTask; }
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("PageVm:routingFrom"); return Task.CompletedTask; }
        public void Dispose() => Disposed = true;
    }

    public sealed class OtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OtherVm : IRouteable
    {
        public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("OtherVm:routedTo"); return Task.CompletedTask; }
        public Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("OtherVm:routedFrom"); return Task.CompletedTask; }
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(Allow);
        public static bool Allow = true;
    }

    private static IRouter Build()
    {
        Log.Clear();
        OtherVm.Allow = true;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<PageView, PageVm>();
        services.RegisterRoute<OtherView, OtherVm>();
        return services.BuildServiceProvider().GetRequiredService<IRouter>();
    }

    private static Task<RouteableResult> GoToPage(IRouter r) =>
        r.GoToRouteAsync<PageVm>(new RouteableEntry<PageVm>("page"), TestContext.Current.CancellationToken);

    private static Task<RouteableResult> GoToOther(IRouter r) =>
        r.GoToRouteAsync<OtherVm>(new RouteableEntry<OtherVm>("other"), TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_first_routing_only_notifies_the_entry_it_enters()
    {
        var router = Build();

        await GoToPage(router);

        Assert.Equal(["PageVm:routingTo", "PageVm:routedTo", "PageView:routedTo"], Log.Where(l => l.Contains("routed") || l.Contains("routing")).ToList());
        Assert.DoesNotContain("routedFrom", string.Join(",", Log));
    }

    [Fact]
    public async Task Routed_callbacks_run_after_the_Routing_ones_and_after_the_history_moved()
    {
        var router = Build();
        await GoToPage(router);
        Log.Clear();

        await GoToOther(router);

        Assert.Equal(
        [
            "PageVm:routingFrom",
            "PageVm:routedFrom", "PageView:routedFrom",
            "OtherVm:routedTo",
        ], Log);
        Assert.IsType<OtherVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Back_notifies_the_entry_left_then_the_entry_returned_to()
    {
        var router = Build();
        await GoToPage(router);
        await GoToOther(router);
        Log.Clear();

        await router.GoBackAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["OtherVm:routedFrom", "PageVm:routedTo", "PageView:routedTo"], Log.Where(l => l.Contains("routed")).ToList());
    }

    [Fact]
    public async Task The_entry_left_is_notified_before_Pop_disposes_it()
    {
        var router = Build();
        await GoToOther(router);
        await GoToPage(router);
        var page = Assert.IsType<PageVm>(router.CurrentViewModel);

        await router.PopAsync(TestContext.Current.CancellationToken);

        Assert.False(page.DisposedWhenRoutedFrom);
        Assert.True(page.Disposed);
    }

    [Fact]
    public async Task A_vetoed_routing_raises_no_Routed_callback()
    {
        var router = Build();
        await GoToOther(router);
        Log.Clear();
        OtherVm.Allow = false;

        var result = await GoToPage(router);

        Assert.False(result.Success);
        Assert.Equal([], Log);
    }

    [Fact]
    public async Task A_throwing_Routed_callback_neither_fails_nor_stops_the_routing()
    {
        var router = Build();
        await GoToOther(router);
        Log.Clear();
        PageVm.ThrowOnEntered = true;
        var raised = new List<RouteableOutcome>();
        router.Routed += (_, e) => raised.Add(e.Outcome);

        RouteableResult result;
        try { result = await GoToPage(router); }
        finally { PageVm.ThrowOnEntered = false; }

        Assert.True(result.Success);
        Assert.Equal([RouteableOutcome.Success], raised);
        Assert.Contains("PageVm:routedTo", string.Join(",", Log));      // the throwing callback did run
        Assert.Contains("PageView:routedTo", string.Join(",", Log));   // and the later one still ran
        Assert.IsType<PageVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_shared_view_and_view_model_is_notified_once_per_entry()
    {
        var calls = new List<string>();
        var shared = new Shared(calls);
        var entry = new RouterEntry(shared, shared, RouteableArgs.Create());
        var other = new RouterEntry(new Shared(calls), new Shared(calls), RouteableArgs.Create());
        var lifecycle = new RouterLifecycle();

        await lifecycle.NotifyEnteredAsync(other, entry, TestContext.Current.CancellationToken);

        Assert.Equal(["from", "from", "to"], calls);
    }

    private sealed class Shared(List<string> calls) : IRouteable
    {
        public Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default) { calls.Add("from"); return Task.CompletedTask; }
        public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default) { calls.Add("to"); return Task.CompletedTask; }
    }
}