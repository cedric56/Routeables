using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.States;
using Routeables.AspNetCore;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;

namespace Routeables.Tests.Core;

/// <summary>Faults found by the review of 11 November: each of these failed before it was fixed.</summary>
public class ReviewFixesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class OtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OtherVm;
    public sealed class BlockedView : IRouteableFor { public object? Context { get; set; } }
    public sealed class BlockedVm;
    // The pages a coordination goes to are found by their view model (a segment), not by an address.
    public sealed class SegView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SegVm;
    public sealed class SegBlockedView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SegBlockedVm;
    public sealed class DirtyView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DirtyVm : IRouteable
    {
        public static bool Dirty;
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(!Dirty);
    }

    public sealed class RefusesBlocked : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(change.To.ViewModel is BlockedVm or SegBlockedVm ? RouteGuardDecision.Cancel : RouteGuardDecision.Allow);
    }

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRouteCoordinator();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<OtherView, OtherVm>(route: "/other");
        services.RegisterRoute<BlockedView, BlockedVm>(route: "/blocked");
        services.RegisterRoute<DirtyView, DirtyVm>(route: "/dirty");
        services.RegisterRoute<SegView, SegVm>();
        services.RegisterRoute<SegBlockedView, SegBlockedVm>();
        services.RegisterSegment<SegBlockedVm>("Blocked");
        services.RegisterSegment<SegVm>("Seg");
        services.AddSingleton<IRouteGuard, RefusesBlocked>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    // ---- H1: restoring the history ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_restoration_whose_first_route_does_not_exist_leaves_the_page_that_was_shown()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);

        var restored = await RouteableState.RestoreAsync(router, ["/gone/42"], Ct);

        Assert.False(restored);
        Assert.Equal(1, router.History.Count);
        Assert.IsType<HomeVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_restoration_whose_first_route_a_guard_refuses_leaves_the_page_that_was_shown()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);

        var restored = await RouteableState.RestoreSnapshotsAsync(router, [new RouteableSnapshot("/blocked", new Dictionary<string, string>())], Ct);

        Assert.False(restored);
        Assert.Equal(1, router.History.Count);
        Assert.IsType<HomeVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_restoration_that_works_still_replaces_the_history()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);

        Assert.True(await RouteableState.RestoreAsync(router, ["/other", "/home"], Ct));

        Assert.Equal(2, router.History.Count);
        Assert.IsType<HomeVm>(router.CurrentViewModel);
    }

    // ---- H2: an absolute coordination ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_absolute_coordination_does_not_erase_a_page_that_refuses_to_be_left()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/dirty", cancellationToken: Ct);
        DirtyVm.Dirty = true;
        try
        {
            var result = await provider.GetRequiredService<IRouteCoordinator>().CoordinateAsync(RouteCoordination.Parse("/Seg"), Ct);

            Assert.False(result.Success);
            Assert.IsType<DirtyVm>(router.CurrentViewModel);       // untouched
            Assert.Equal(1, router.History.Count);
        }
        finally { DirtyVm.Dirty = false; }
    }

    [Fact]
    public async Task An_absolute_coordination_that_fails_on_its_first_segment_does_not_leave_the_region_empty()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);

        var result = await provider.GetRequiredService<IRouteCoordinator>().CoordinateAsync(RouteCoordination.Parse("/Blocked"), Ct);

        Assert.False(result.Success);
        Assert.Equal(1, router.History.Count);
        Assert.IsType<HomeVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task An_absolute_coordination_that_works_still_starts_from_an_empty_history()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        await router.GoToAsync("/other", cancellationToken: Ct);

        var result = await provider.GetRequiredService<IRouteCoordinator>().CoordinateAsync(RouteCoordination.Parse("/Seg"), Ct);

        Assert.True(result.Success, result.Exception?.ToString());
        Assert.Equal(1, router.History.Count);
    }

    // ---- M1 and Finding 4: the interactions of the web host ---------------------------------------------------------------------

    [Fact]
    public async Task A_button_that_does_not_exist_is_not_an_answer()
    {
        using var interactions = new RouteableInteractions();
        var dialog = new RouteableDialog("Title", null, "Body", ["Yes", "No"]);
        var answer = interactions.AskAsync(dialog);

        await interactions.AnswerAsync(7).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await interactions.AnswerAsync(-1).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.False(answer.IsCompleted);
        Assert.Same(dialog, interactions.Pending);

        await interactions.AnswerAsync(1);
        Assert.Equal(1, await answer);
    }

    [Fact]
    public void The_toasts_a_page_reads_do_not_change_under_it()
    {
        using var interactions = new RouteableInteractions();
        interactions.Notify("A", "a", RouteableNotificationType.Information, null, null, null);
        var seen = interactions.Toasts;

        interactions.Notify("B", "b", RouteableNotificationType.Information, null, null, null);

        Assert.Single(seen);
        Assert.Equal(2, interactions.Toasts.Count);
    }

    [Fact]
    public async Task Requests_of_one_visitor_can_change_the_notifications_at_the_same_time()
    {
        using var interactions = new RouteableInteractions();

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            for (var n = 0; n < 200; n++)
            {
                interactions.Notify("t", "m", RouteableNotificationType.Information, null, null, null);
                _ = interactions.Toasts.Count;
                interactions.Dismiss(-1, click: false);
                var system = interactions.CreateSystemNotification(null);
                _ = interactions.ActiveSystemNotifications.Count;
                interactions.TakeSystemNotificationsJson();
                interactions.CloseSystemNotification(system.Id);
            }
        }, Ct)));

        Assert.Equal(64 * 200, interactions.Toasts.Count);
    }

    // ---- M3: the case of a value in an address ----------------------------------------------------------------------------------

    private static readonly (string Template, Type? ViewModelType)[] Items = [("/items/{id}", null), ("/orders", null)];

    [Fact]
    public void The_case_of_a_value_makes_another_page_and_the_case_of_a_word_does_not()
    {
        Assert.False(RouteableUrlSync.Same("/items/aB", "/items/Ab", Items));      // a token, a slug: two pages
        Assert.True(RouteableUrlSync.Same("/Orders", "/orders", Items));           // a word of the route
        Assert.True(RouteableUrlSync.Same("/ITEMS/aB", "/items/aB", Items));
        Assert.True(RouteableUrlSync.Same("/items/aB", "/items/aB", Items));
        Assert.True(RouteableUrlSync.Same("/Whatever", "/whatever"));              // no route is known: nothing to protect
    }

    // ---- M6 and L4: the registrations -------------------------------------------------------------------------------------------

    [Fact]
    public void Routes_added_after_a_region_still_register_the_default_router()
    {
        var services = new ServiceCollection();
        services.AddRegion("side");

        services.AddRoutes(_ => { });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRouter>());
    }

    [Fact]
    public void The_history_of_a_scoped_region_is_limited_like_the_one_of_a_scoped_router()
    {
        var services = new ServiceCollection();
        services.AddRegion("side");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal(Routeables.Routing.Extensions.DefaultScopedHistoryLimit, scope.ServiceProvider.GetRequiredKeyedService<RouterHistory>("side").MaxSize);
    }

    // ---- L5: a fragment is not part of a route ------------------------------------------------------------------------------------

    [Fact]
    public void A_fragment_is_left_out_of_the_route()
    {
        Assert.True(RouteParser.TryMatch("/orders/{id:int}", "/orders/42#top"));
        Assert.True(RouteParser.TryMatch("/orders/{id:int}", "/orders/42?x=1#top"));
        Assert.False(RouteParser.TryMatch("/orders/{id:int}", "/orders/4x2#top"));
    }

    // ---- M7: a page is disposed once ------------------------------------------------------------------------------------------------

    public sealed class CountedView : IRouteableFor { public object? Context { get; set; } }
    public sealed class CountedVm : IDisposable
    {
        public static int Disposed;
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    [Fact]
    public async Task A_transient_page_the_router_disposed_is_not_disposed_again_at_the_end_of_the_scope()
    {
        CountedVm.Disposed = 0;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<CountedView, CountedVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/counted");
        services.AddTransient<CountedVm>();
        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/counted", cancellationToken: Ct);

        await router.EraseHistoryAsync(Ct);
        Assert.Equal(1, CountedVm.Disposed);

        await provider.DisposeAsync();

        Assert.Equal(1, CountedVm.Disposed);
    }

    // ---- Finding 5: the addresses that wait for the application -------------------------------------------------------------------

    [Fact]
    public void Addresses_that_wait_for_the_first_page_are_limited()
    {
        var inbox = new RouteableLinkInbox(["myapp"]);

        for (var i = 0; i < 1000; i++)
            inbox.Push(new Uri($"myapp://orders/{i}"));
        inbox.Push(new Uri("myapp://orders/" + new string('x', RouteableDeepLinks.MaxLength + 1)));

        Assert.Equal(RouteableLinkInbox.MaxPending, inbox.PendingCount);
    }

    // ---- Finding 6: the order of the saves of the history ---------------------------------------------------------------------------

    public sealed class SlowView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SlowVm : IRouteableState
    {
        public static readonly ManualResetEventSlim Reading = new();
        public static readonly ManualResetEventSlim Release = new();
        public static int Calls;

        public void SaveState(IDictionary<string, string> state)
        {
            if (Interlocked.Increment(ref Calls) == 1)
            {
                Reading.Set();      // the first read of the history is slow
                Release.Wait(TimeSpan.FromSeconds(10));
            }
        }

        public void LoadState(IReadOnlyDictionary<string, string> state) { }
    }

    private sealed class MemoryStore : IRouteableHistoryStore
    {
        public string? Kept { get; private set; }
        public string? Load() => null;
        public void Save(string text) => Kept = text;
    }

    [Fact]
    public async Task A_save_that_read_the_history_before_another_one_does_not_replace_it()
    {
        SlowVm.Reading.Reset();
        SlowVm.Release.Reset();
        SlowVm.Calls = 1;                                   // not the first read: the save of the first route change must not wait
        var store = new MemoryStore();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<SlowView, SlowVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/slow");
        services.RegisterRoute<OtherView, OtherVm>(route: "/other");
        services.AddTransient<SlowVm>();
        services.AddSingleton<IRouteableHistoryStore>(store);
        services.AddRouteableHistory();
        await using var provider = services.BuildServiceProvider();
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/slow", cancellationToken: Ct);
        SlowVm.Calls = 0;                                   // the save of that route change is done

        var keeper = provider.GetRequiredService<RouteableHistoryKeeper>();
        var slow = Task.Run(keeper.Flush, Ct);              // reads the history [/slow] and waits in SaveState
        Assert.True(SlowVm.Reading.Wait(TimeSpan.FromSeconds(10), Ct));

        await router.GoToAsync("/other", cancellationToken: Ct);   // its save reads [/slow, /other] and writes it
        SlowVm.Release.Set();
        await slow;

        Assert.True(new[] { "/slow", "/other" }.SequenceEqual(RouteableState.Deserialize(store.Kept)));
    }
}
