using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Core;

/// <summary>The Routing / Routed events of <see cref="IRouter"/> and <see cref="IRouterHistory.Changed"/>.</summary>
public class RouterEventsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Subscribes to both events and records them in <paramref name="log"/> and in the returned lists.</summary>
    private static (List<RouteableRoutingEventArgs> Routing, List<RouteableRoutedEventArgs> Routed) Observe(IRouter router, CallLog? log = null)
    {
        var routing = new List<RouteableRoutingEventArgs>();
        var routed = new List<RouteableRoutedEventArgs>();
        router.Routing += (_, e) => { routing.Add(e); log?.Add($"event:Routing:{e.Kind}"); };
        router.Routed += (_, e) => { routed.Add(e); log?.Add($"event:Routed:{e.Kind}:{e.Outcome}"); };
        return (routing, routed);
    }

    [Fact]
    public async Task Routing_IsRaisedBeforeAnyCallback_AndRoutedAfterTheLastOne()
    {
        var router = CreateRouter();
        var log = new CallLog();
        Observe(router, log);

        await GoToAsync(router, "A", log);

        Assert.Equal(
        [
            "event:Routing:Push",
            "vmA:Init", "viewA:Init",
            "vmA:To", "viewA:To",
            "event:Routed:Push:Success",
        ], log.Entries);
    }

    [Fact]
    public async Task Routed_IsRaisedExactlyOnce_AfterEveryRouting()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var (routing, routed) = Observe(router);
        await GoToAllAsync(router, log, "A", "B");
        await router.GoBackAsync(Ct);

        Assert.Equal(3, routing.Count);
        Assert.Equal(3, routed.Count);
    }

    [Fact]
    public async Task TheKind_IsReported_ForEveryOperation()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var (_, routed) = Observe(router);

        await GoToAllAsync(router, log, "A", "B", "C");              // Push x3
        await router.GoBackAsync(Ct);                                  // Back          (B)
        await router.ForwardAsync(Ct);                               // Forward       (C)
        await router.MoveToAsync(0, Ct);                             // MoveTo        (A)
        await router.MoveToAsync(2, Ct);                             // MoveTo        (C)
        await router.PopAsync(Ct);                                   // Pop           (B)
        await router.GoBackToRootAsync(Ct);                            // Root          (A)

        Assert.Equal(
        [
            RouteableKind.Push, RouteableKind.Push, RouteableKind.Push,
            RouteableKind.Back, RouteableKind.Forward,
            RouteableKind.MoveTo, RouteableKind.MoveTo,
            RouteableKind.Pop, RouteableKind.Root,
        ], routed.Select(e => e.Kind).ToList());
    }

    [Fact]
    public async Task Routing_ReportsTheEntryBeingLeftAndTheOneBeingEntered()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var (routing, routed) = Observe(router);

        var a = await GoToAsync(router, "A", log);
        var b = await GoToAsync(router, "B", log);

        Assert.Null(routing[0].From);                                // first route change: nothing is left
        Assert.Same(a.ViewModel, routing[0].To.ViewModel);
        Assert.Same(a.View, routing[0].To.View);

        Assert.Same(a.ViewModel, routing[1].From!.ViewModel);
        Assert.Same(b.ViewModel, routing[1].To.ViewModel);
        Assert.Same(b.ViewModel, routed[1].To.ViewModel);
    }

    [Fact]
    public async Task Routed_ReportsSuccess()
    {
        var router = CreateRouter();
        var (_, routed) = Observe(router);

        await GoToAsync(router, "A", new CallLog());

        var e = Assert.Single(routed);
        Assert.Equal(RouteableOutcome.Success, e.Outcome);
        Assert.True(e.Succeeded);
        Assert.Null(e.Exception);
    }

    [Fact]
    public async Task Routed_ReportsCancelled_WhenAGuardRefuses()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        a.ViewModel.CanLeave = false;
        var (_, routed) = Observe(router);

        var result = await router.GoAsync(new RecordingView("viewB", log), new RecordingViewModel("vmB", log), cancellationToken: Ct);

        Assert.True(result.Cancelled);
        var e = Assert.Single(routed);
        Assert.Equal(RouteableOutcome.Cancelled, e.Outcome);
        Assert.False(e.Succeeded);
        Assert.Null(e.Exception);
        Assert.Equal(1, router.History.Count);
    }

    [Fact]
    public async Task Routed_ReportsFailed_WithTheException_WhenACallbackThrows()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var (_, routed) = Observe(router);
        var failure = new InvalidOperationException("enter failed");
        var view = new RecordingView("viewA", log);
        var viewModel = new RecordingViewModel("vmA", log) { EnterFails = failure };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoAsync(view, viewModel, cancellationToken: Ct));

        Assert.Same(failure, thrown);
        var e = Assert.Single(routed);
        Assert.Equal(RouteableOutcome.Failed, e.Outcome);
        Assert.Same(failure, e.Exception);
        Assert.Null(router.CurrentViewModel);
        Assert.Null(view.Context);                                   // restored before Routed was raised
    }

    [Fact]
    public async Task Routed_ReportsCancelled_WhenTheTokenIsCancelled()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var (routing, routed) = Observe(router);
        using var cts = new CancellationTokenSource();
        var viewModel = new RecordingViewModel("vmA", log) { OnEnter = ct => Task.Delay(Timeout.Infinite, ct) };

        var pending = router.GoAsync(new RecordingView("viewA", log), viewModel, cancellationToken: cts.Token);
        Assert.False(pending.IsCompleted);                        // parked inside OnEntering
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Single(routing);
        var e = Assert.Single(routed);
        Assert.Equal(RouteableOutcome.Cancelled, e.Outcome);
        Assert.Equal(0, router.History.Count);
    }

    [Fact]
    public async Task NoEvent_IsRaised_WhenThereIsNothingToDo()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var (routing, routed) = Observe(router);

        await router.GoBackAsync(Ct);                                  // empty
        await router.ForwardAsync(Ct);
        await router.PopAsync(Ct);
        await router.GoBackToRootAsync(Ct);
        await router.MoveToAsync(0, Ct);

        await GoToAsync(router, "A", log);
        routing.Clear();
        routed.Clear();

        await router.GoBackAsync(Ct);                                  // a single entry
        await router.PopAsync(Ct);
        await router.GoBackToRootAsync(Ct);
        await router.MoveToAsync(0, Ct);                             // the current entry
        await router.MoveToAsync(5, Ct);                             // out of range

        Assert.Empty(routing);
        Assert.Empty(routed);
    }

    [Fact]
    public async Task AThrowingHandler_DoesNotAffectTheRouting()
    {
        var router = CreateRouter();
        router.Routing += (_, _) => throw new InvalidOperationException("routing handler");
        router.Routed += (_, _) => throw new InvalidOperationException("routed handler");
        var healthy = new List<RouteableRoutedEventArgs>();
        router.Routed += (_, e) => healthy.Add(e);                   // the next handler must still be called

        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct);

        Assert.True(result.Success);
        Assert.Equal(1, router.History.Count);
        Assert.Single(healthy);
    }

    [Fact]
    public async Task AHandler_CannotStartARouting()
    {
        var router = CreateRouter();
        Task<RouteableResult>? nested = null;
        router.Routing += (_, _) => nested = router.GoBackAsync(Ct);   // the gate is held while handlers run

        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct);

        Assert.True(result.Success);
        Assert.NotNull(nested);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => nested!);
        Assert.Contains("Reentrant", ex.Message);
    }

    // ------------------------------------------------------------- History.Changed

    [Fact]
    public async Task HistoryChanged_IsRaisedOncePerChange_AndNotForNoOps()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var changes = 0;
        router.History.Changed += (_, _) => changes++;

        async Task ExpectAsync(int expected, Func<Task> act)
        {
            changes = 0;
            await act();
            Assert.Equal(expected, changes);
        }

        await ExpectAsync(0, () => router.GoBackAsync(Ct));                        // no-op
        await ExpectAsync(1, () => GoToAsync(router, "A", log));                 // push
        await ExpectAsync(1, () => GoToAsync(router, "B", log));
        await ExpectAsync(1, () => GoToAsync(router, "C", log));
        await ExpectAsync(1, () => router.GoBackAsync(Ct));
        await ExpectAsync(1, () => router.ForwardAsync(Ct));
        await ExpectAsync(1, () => router.MoveToAsync(0, Ct));
        await ExpectAsync(0, () => router.MoveToAsync(0, Ct));                   // no-op: already there
        await ExpectAsync(1, () => router.MoveToAsync(2, Ct));
        await ExpectAsync(1, () => router.PopAsync(Ct));
        await ExpectAsync(1, () => router.GoBackToRootAsync(Ct));
        await ExpectAsync(1, () => router.EraseHistoryAsync(Ct));
    }

    [Fact]
    public async Task HistoryChanged_IsNotRaised_WhenAGuardCancelsTheRouting()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        a.ViewModel.CanLeave = false;
        var changes = 0;
        router.History.Changed += (_, _) => changes++;

        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct);

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task HistoryEntries_IsASnapshot_OldestFirst()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");

        var snapshot = router.History.Entries;
        await GoToAsync(router, "C", log);

        Assert.Equal(2, snapshot.Count);                             // the later push is not visible in the copy
        Assert.Same(pages[0].ViewModel, snapshot[0].ViewModel);
        Assert.Same(pages[1].ViewModel, snapshot[1].ViewModel);
        Assert.Equal(3, router.History.Count);
        Assert.Equal(2, router.History.CurrentIndex);
        Assert.Same(pages[0].ViewModel, router.History.Root!.ViewModel);
        Assert.Equal(20, router.History.MaxSize);
    }
}
