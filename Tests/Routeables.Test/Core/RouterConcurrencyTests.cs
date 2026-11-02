using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Page = Routeables.Test.Abstraction.Page;

namespace Routeables.Tests.Core;

/// <summary>
/// Several callers routing at the same moment, through the real <see cref="Router"/>: the router runs one change at a time, in the
/// order it was asked, a caller that gives up does not block the others, and a hook that routes again does not deadlock.
/// </summary>
public class RouterConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Page NewPage(string name, CallLog log)
        => new(new RecordingViewModel($"vm{name}", log), new RecordingView($"view{name}", log));

    // The gate remembers the flow that holds it: each caller below is a flow of its own, as two events of a user interface are.
    private static Task<T> Flow<T>(Func<Task<T>> work) => Task.Run(work, Ct);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition was not met in time.");
            await Task.Delay(10, Ct);
        }
    }

    private static TaskCompletionSource NewRelease() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task A_second_route_waits_until_the_first_one_is_finished()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var release = NewRelease();
        var b = NewPage("B", log);
        b.ViewModel.OnEnter = _ => release.Task;
        var c = NewPage("C", log);

        var first = Flow(() => router.GoAsync(b.View, b.ViewModel, null, Ct));
        await WaitUntilAsync(() => log.Entries.Contains("vmB:To"));
        var second = Flow(() => router.GoAsync(c.View, c.ViewModel, null, Ct));
        await Task.Delay(100, Ct);

        Assert.False(second.IsCompleted);
        Assert.DoesNotContain("vmC:Init", log.Entries);    // C has not started: B is still entering

        release.SetResult();
        Assert.True((await first.WaitAsync(Timeout, Ct)).Success);
        Assert.True((await second.WaitAsync(Timeout, Ct)).Success);

        var entries = log.Entries.ToList();
        var bEntered = Math.Max(entries.IndexOf("vmB:To"), entries.IndexOf("viewB:To"));
        Assert.True(bEntered < entries.IndexOf("vmB:CanLeave"));    // C asks B whether it can leave only once B has been entered
        Assert.True(bEntered < entries.IndexOf("vmC:Init"));
        Assert.Same(c.ViewModel, router.CurrentViewModel);
        Assert.Equal(3, router.History.Count);
    }

    [Fact]
    public async Task Going_back_asked_during_a_route_goes_back_from_the_new_page()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        var release = NewRelease();
        var b = NewPage("B", log);
        b.ViewModel.OnEnter = _ => release.Task;

        var route = Flow(() => router.GoAsync(b.View, b.ViewModel, null, Ct));
        await WaitUntilAsync(() => log.Entries.Contains("vmB:To"));
        var back = Flow(() => router.GoBackAsync(Ct));
        await Task.Delay(100, Ct);
        Assert.False(back.IsCompleted);

        release.SetResult();
        Assert.True((await route.WaitAsync(Timeout, Ct)).Success);
        Assert.True((await back.WaitAsync(Timeout, Ct)).Success);

        Assert.Same(a.ViewModel, router.CurrentViewModel);    // B was entered, then left
        Assert.Equal(2, router.History.Count);
        Assert.True(router.CanGoForward);
        Assert.Equal(0, b.ViewModel.DestroyCount);             // B stays in the history, to go forward to
    }

    [Fact]
    public async Task Many_simultaneous_routes_never_overlap_and_keep_every_page_once()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var first = await GoToAsync(router, "A", log);
        var inFlight = 0;
        var maxInFlight = 0;
        var gate = new object();

        var pages = Enumerable.Range(0, 12).Select(i =>
        {
            var page = NewPage($"P{i}", log);
            page.ViewModel.OnEnter = async ct =>
            {
                lock (gate) { inFlight++; maxInFlight = Math.Max(maxInFlight, inFlight); }
                await Task.Yield();
                await Task.Delay(1, ct);
                lock (gate) { inFlight--; }
            };
            return page;
        }).ToList();

        var results = await Task.WhenAll(pages.Select(page => Flow(() => router.GoAsync(page.View, page.ViewModel, null, Ct))))
            .WaitAsync(Timeout, Ct);

        Assert.All(results, result => Assert.True(result.Success));
        Assert.Equal(1, maxInFlight);                           // never two pages entering at the same time
        Assert.Equal(13, router.History.Count);                 // A and the twelve, none lost, none twice
        Assert.All(pages.Append(first), page =>
        {
            Assert.Equal(1, page.ViewModel.InitializeCount);
            Assert.Equal(0, page.ViewModel.DestroyCount);
        });
    }

    [Fact]
    public async Task A_waiting_route_that_is_cancelled_does_not_block_the_others()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var release = NewRelease();
        var b = NewPage("B", log);
        b.ViewModel.OnEnter = _ => release.Task;
        var c = NewPage("C", log);
        var d = NewPage("D", log);
        using var cancel = new CancellationTokenSource();

        var first = Flow(() => router.GoAsync(b.View, b.ViewModel, null, Ct));
        await WaitUntilAsync(() => log.Entries.Contains("vmB:To"));
        var waiting = Flow(() => router.GoAsync(c.View, c.ViewModel, null, cancel.Token));
        await Task.Delay(100, Ct);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Timeout, Ct));
        release.SetResult();
        Assert.True((await first.WaitAsync(Timeout, Ct)).Success);
        Assert.True((await Flow(() => router.GoAsync(d.View, d.ViewModel, null, Ct)).WaitAsync(Timeout, Ct)).Success);

        Assert.DoesNotContain("vmC:Init", log.Entries);         // the cancelled route never started
        Assert.Same(d.ViewModel, router.CurrentViewModel);
        Assert.Equal(3, router.History.Count);                  // A, B, D
    }

    [Fact]
    public async Task Cancelling_the_route_in_progress_lets_the_next_one_run()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        var b = NewPage("B", log);
        b.ViewModel.OnEnter = ct => Task.Delay(System.Threading.Timeout.Infinite, ct);
        using var cancel = new CancellationTokenSource();

        var running = Flow(() => router.GoAsync(b.View, b.ViewModel, null, cancel.Token));
        await WaitUntilAsync(() => log.Entries.Contains("vmB:To"));
        await cancel.CancelAsync();
        Exception? outcome = null;
        try { await running.WaitAsync(Timeout, Ct); }
        catch (Exception ex) { outcome = ex; }

        Assert.True(outcome is null or OperationCanceledException, $"Unexpected: {outcome}");
        var c = await GoToAsync(router, "C", log);              // the gate was released: this one does not wait for ever
        Assert.Same(c.ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);                  // A and C: B never entered the history
        Assert.Equal(0, a.ViewModel.DestroyCount);
    }

    [Fact]
    public async Task A_hook_that_routes_again_fails_instead_of_waiting_for_ever()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        var b = NewPage("B", log);
        var nested = NewPage("N", log);
        b.ViewModel.OnEnter = _ => router.GoAsync(nested.View, nested.ViewModel, null, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoAsync(b.View, b.ViewModel, null, Ct).WaitAsync(Timeout, Ct));

        Assert.Same(a.ViewModel, router.CurrentViewModel);      // the failed route left the history alone
        Assert.Equal(1, router.History.Count);
        var c = await GoToAsync(router, "C", log);              // and the router still works
        Assert.Same(c.ViewModel, router.CurrentViewModel);
    }
}
