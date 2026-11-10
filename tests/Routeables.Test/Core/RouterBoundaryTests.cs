using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Core;

/// <summary>The edges between the router and what is around it: observers of the history that throw, guards that refresh while a route change runs.</summary>
public class RouterBoundaryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_history_observer_that_throws_does_not_fail_the_route_change()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var later = 0;
        router.History.Changed += (_, _) => throw new InvalidOperationException("observer failed");
        router.History.Changed += (_, _) => later++;      // the observers after the one that failed are still called

        var second = await GoToAsync(router, "B", log);    // asserts Success

        Assert.Same(second.ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
        Assert.Equal(1, later);
    }

    [Fact]
    public async Task A_history_observer_that_throws_does_not_break_back_pop_or_erase()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C");
        router.History.Changed += (_, _) => throw new InvalidOperationException("observer failed");

        Assert.True((await router.GoBackAsync(Ct)).Success);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.True((await router.PopAsync(Ct)).Success);
        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);

        await router.EraseHistoryAsync(Ct);                // must not throw
        Assert.Equal(0, router.History.Count);
    }

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class OtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OtherVm;
    public sealed class LoginView : IRouteableFor { public object? Context { get; set; } }
    public sealed class LoginVm;

    // A guard that waits before it answers, so that a route change can come in between.
    private sealed class SlowGuard : IRouteGuard
    {
        public volatile bool Slow;
        public volatile bool Deny;
        public TaskCompletionSource? BlockOther;
        public int Running, MaxRunning;
        public List<string> Asked { get; } = [];
        public TaskCompletionSource Release { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref Running);
            lock (Asked) { MaxRunning = Math.Max(MaxRunning, now); Asked.Add(change.To.Args.Route ?? "?"); }
            try
            {
                var route = change.To.Args.Route?.Trim('/');
                if (route == "other" && BlockOther is { } block) await block.Task.WaitAsync(cancellationToken);
                if (route == "login" || !Deny) return RouteGuardDecision.Allow;
                if (Slow) await Release.Task.WaitAsync(cancellationToken);
                return RouteGuardDecision.Redirect("/login");
            }
            finally { Interlocked.Decrement(ref Running); }
        }
    }

    private static (ServiceProvider provider, SlowGuard guard) Build()
    {
        var guard = new SlowGuard();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<OtherView, OtherVm>(route: "/other");
        services.RegisterRoute<LoginView, LoginVm>(route: "/login");
        services.AddSingleton<IRouteGuard>(guard);
        return (services.BuildServiceProvider(), guard);
    }

    [Fact]
    public async Task A_refresh_waits_for_the_route_change_that_is_running_and_asks_about_the_page_then_shown()
    {
        var (provider, guard) = Build();
        await using var owner = provider;
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        guard.BlockOther = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (guard.Asked) guard.Asked.Clear();

        var change = router.GoToAsync("/other", cancellationToken: Ct);     // its guard waits
        await Task.Delay(50, Ct);
        var refresh = router.RefreshGuardsAsync(Ct);
        await Task.Delay(50, Ct);
        Assert.False(refresh.IsCompleted);                                  // it did not start in the middle of the route change

        guard.BlockOther.SetResult();
        await change;
        await refresh;

        Assert.IsType<OtherVm>(router.CurrentViewModel);
        lock (guard.Asked) Assert.Equal(["other", "other"], guard.Asked.Select(a => a.Trim('/')).ToArray());   // the route change, then the refresh: about the page shown then
    }

    [Fact]
    public async Task Two_refreshes_at_once_never_run_the_guards_at_the_same_time()
    {
        var (provider, guard) = Build();
        await using var owner = provider;
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        guard.Deny = true;
        guard.Slow = true;

        var first = router.RefreshGuardsAsync(Ct);
        var second = router.RefreshGuardsAsync(Ct);
        await Task.Delay(50, Ct);
        guard.Release.SetResult();
        guard.Slow = false;
        await Task.WhenAll(first, second);

        Assert.Equal(1, guard.MaxRunning);
        Assert.IsType<LoginVm>(router.CurrentViewModel);
    }
}
