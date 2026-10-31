using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Page = Routeables.Test.Abstraction.Page;

namespace Routeables.Tests.Core;

/// <summary>
/// Scenarios that mix several routers (one per region), failures, cancellations and the history, through the real
/// <see cref="Router"/>: what stays displayed, what stays in the history and what is destroyed when something goes wrong halfway.
/// </summary>
public class RouterLifecycleScenarioTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Page NewPage(string name, CallLog log)
        => new(new RecordingViewModel($"vm{name}", log), new RecordingView($"view{name}", log));

    /// <summary>A page the router disposes when it leaves the history: counts how many times that happened.</summary>
    private sealed class DisposablePage : IRouteable, IDisposable
    {
        public int Disposed { get; private set; }
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() => Disposed++;
    }

    private static Task<RouteableResult> Go(Router router, Page page)
        => Task.Run(() => router.GoAsync(page.View, page.ViewModel, null, Ct), Ct);

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

    [Fact]
    public async Task A_router_that_is_busy_does_not_block_another_one()
    {
        // One router per region: a slow page in one region must not freeze the others.
        var slowRouter = CreateRouter();
        var otherRouter = CreateRouter();
        var log = new CallLog();
        await GoToAsync(slowRouter, "A", log);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = NewPage("Slow", log);
        slow.ViewModel.OnEnter = _ => release.Task;

        var slowRoute = Go(slowRouter, slow);
        await WaitUntilAsync(() => log.Entries.Contains("vmSlow:To"));

        var other = NewPage("Other", log);
        var result = await Go(otherRouter, other).WaitAsync(Timeout, Ct);

        Assert.True(result.Success);
        Assert.Same(other.ViewModel, otherRouter.CurrentViewModel);
        Assert.False(slowRoute.IsCompleted);

        release.SetResult();
        Assert.True((await slowRoute.WaitAsync(Timeout, Ct)).Success);
    }

    [Fact]
    public async Task Routers_used_together_each_keep_their_own_history()
    {
        var routers = Enumerable.Range(0, 4).Select(_ => CreateRouter()).ToArray();
        var log = new CallLog();

        await Task.WhenAll(routers.Select((router, index) => Task.Run(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                var page = NewPage($"R{index}P{i}", log);
                Assert.True((await router.GoAsync(page.View, page.ViewModel, null, Ct)).Success);
            }
        }, Ct))).WaitAsync(Timeout, Ct);

        for (var index = 0; index < routers.Length; index++)
        {
            Assert.Equal(10, routers[index].History.Count);
            Assert.Equal($"vmR{index}P9", ((RecordingViewModel)routers[index].CurrentViewModel!).Name);
        }
    }

    [Fact]
    public async Task A_page_that_fails_to_enter_does_not_replace_the_current_page()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        var broken = NewPage("Broken", log);
        broken.ViewModel.EnterFails = new InvalidOperationException("enter failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoAsync(broken.View, broken.ViewModel, null, Ct));

        Assert.Same(a.ViewModel, router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        Assert.False(router.CanGoBack);
        Assert.Equal(0, a.ViewModel.DestroyCount);
    }

    [Fact]
    public async Task A_page_that_fails_to_initialize_does_not_replace_the_current_page()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        var broken = NewPage("Broken", log);
        broken.ViewModel.InitializeFails = new InvalidOperationException("init failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoAsync(broken.View, broken.ViewModel, null, Ct));

        Assert.Same(a.ViewModel, router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        Assert.Equal(0, a.ViewModel.DestroyCount);
    }

    [Fact]
    public async Task The_history_can_be_navigated_after_a_failed_routing()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");
        var broken = NewPage("Broken", log);
        broken.ViewModel.EnterFails = new InvalidOperationException("enter failed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoAsync(broken.View, broken.ViewModel, null, Ct));

        var back = await router.GoBackAsync(Ct).WaitAsync(Timeout, Ct);
        var forward = await router.ForwardAsync(Ct).WaitAsync(Timeout, Ct);

        Assert.True(back.Success);
        Assert.True(forward.Success);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public async Task A_page_that_refuses_to_be_left_keeps_the_current_page_and_the_history()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        a.ViewModel.CanLeave = false;
        var b = NewPage("B", log);

        var result = await router.GoAsync(b.View, b.ViewModel, null, Ct);

        Assert.False(result.Success);
        Assert.Same(a.ViewModel, router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        Assert.DoesNotContain("vmB:To", log.Entries);

        a.ViewModel.CanLeave = true;
        Assert.True((await router.GoAsync(b.View, b.ViewModel, null, Ct)).Success);
        Assert.Same(b.ViewModel, router.CurrentViewModel);
    }

    [Fact]
    public async Task A_page_that_refuses_to_be_left_also_blocks_going_back()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");
        pages[1].ViewModel.CanLeave = false;

        var result = await router.GoBackAsync(Ct);

        Assert.False(result.Success);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task Cancelling_while_a_page_is_entering_leaves_the_router_usable()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var slow = NewPage("Slow", log);
        slow.ViewModel.OnEnter = ct => Task.Delay(Timeout, ct);

        var route = Task.Run(() => router.GoAsync(slow.View, slow.ViewModel, null, cts.Token), Ct);
        await WaitUntilAsync(() => log.Entries.Contains("vmSlow:To"));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => route.WaitAsync(Timeout, Ct));

        var next = NewPage("Next", log);
        Assert.True((await router.GoAsync(next.View, next.ViewModel, null, Ct).WaitAsync(Timeout, Ct)).Success);
        Assert.Same(next.ViewModel, router.CurrentViewModel);
    }

    [Fact]
    public async Task Routing_from_a_second_router_inside_a_hook_does_not_wait_for_the_first_one()
    {
        // A page of one region that sends another region somewhere: the two gates are independent.
        var main = CreateRouter();
        var side = CreateRouter();
        var log = new CallLog();
        await GoToAsync(main, "A", log);
        var sidePage = NewPage("Side", log);
        var page = NewPage("B", log);
        page.ViewModel.OnEnter = async _ =>
        {
            var inner = await side.GoAsync(sidePage.View, sidePage.ViewModel, null, Ct);
            Assert.True(inner.Success);
        };

        var result = await main.GoAsync(page.View, page.ViewModel, null, Ct).WaitAsync(Timeout, Ct);

        Assert.True(result.Success);
        Assert.Same(page.ViewModel, main.CurrentViewModel);
        Assert.Same(sidePage.ViewModel, side.CurrentViewModel);
    }

    [Fact]
    public async Task Going_back_then_routing_elsewhere_disposes_the_forward_pages_once()
    {
        var router = CreateRouter();
        var pages = new[] { new DisposablePage(), new DisposablePage(), new DisposablePage() };
        foreach (var page in pages)
            Assert.True((await router.GoAsync(new TestView(), page, null, Ct)).Success);
        await router.GoBackAsync(Ct);
        await router.GoBackAsync(Ct);
        var d = new DisposablePage();

        Assert.True((await router.GoAsync(new TestView(), d, null, Ct)).Success);

        Assert.Equal(2, router.History.Count);
        Assert.Equal(0, pages[0].Disposed);
        Assert.Equal(1, pages[1].Disposed);
        Assert.Equal(1, pages[2].Disposed);
        Assert.Equal(0, d.Disposed);
    }

    [Fact]
    public async Task Mixed_routes_back_and_forward_asked_together_never_dispose_the_current_page()
    {
        var router = CreateRouter();
        var pages = new List<DisposablePage>();
        for (var i = 0; i < 3; i++)
        {
            pages.Add(new DisposablePage());
            Assert.True((await router.GoAsync(new TestView(), pages[^1], null, Ct)).Success);
        }

        var work = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            var page = new DisposablePage();
            pages.Add(page);
            work.Add(Task.Run(() => router.GoAsync(new TestView(), page, null, Ct), Ct));
            work.Add(Task.Run(() => router.GoBackAsync(Ct), Ct));
            work.Add(Task.Run(() => router.ForwardAsync(Ct), Ct));
        }

        await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(20), Ct);

        // Whatever the order, the router ends on a page of the history that is still alive, and nothing is disposed twice.
        var current = Assert.IsType<DisposablePage>(router.CurrentViewModel);
        Assert.Equal(0, current.Disposed);
        Assert.InRange(router.History.Count, 1, pages.Count);
        Assert.All(pages, p => Assert.InRange(p.Disposed, 0, 1));
    }
}
