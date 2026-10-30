using System.Diagnostics;
using System.Diagnostics.Metrics;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>A <see cref="IRouteGuard"/> is asked before every route change of a router; it lets it go on, cancels it, or sends the user elsewhere.</summary>
public class RouteGuardTests
{
    public sealed class GuardHomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class GuardHomeVm;
    public sealed class GuardAdminView : IRouteableFor { public object? Context { get; set; } }
    public sealed class GuardAdminVm;
    public sealed class GuardLoginView : IRouteableFor { public object? Context { get; set; } }
    public sealed class GuardLoginVm;

    private sealed class Guard(Func<RouteableRoutingEventArgs, RouteGuardDecision> decide, List<string>? asked = null) : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
        {
            asked?.Add($"{change.Kind} {change.To.Args.Route}");
            return Task.FromResult(decide(change));
        }
    }

    private sealed class ThrowingGuard : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("the guard failed");
    }

    private static ServiceProvider Build(params IRouteGuard[] guards)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<GuardHomeView, GuardHomeVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/guard-home");
        services.RegisterRoute<GuardAdminView, GuardAdminVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/guard-admin");
        services.RegisterRoute<GuardLoginView, GuardLoginVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/guard-login");
        services.AddTransient<GuardHomeVm>();
        services.AddTransient<GuardAdminVm>();
        services.AddTransient<GuardLoginVm>();
        foreach (var guard in guards)
            services.AddSingleton(guard);
        return services.BuildServiceProvider();
    }

    private static bool IsAdmin(RouteableRoutingEventArgs change) => change.To.ViewModel is GuardAdminVm;

    [Fact]
    public async Task Without_a_guard_the_route_change_goes_on()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/guard-admin", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.IsType<GuardAdminVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_guard_that_cancels_leaves_the_page_and_the_history_as_they_were_and_the_router_free()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(new Guard(c => IsAdmin(c) ? RouteGuardDecision.Cancel : RouteGuardDecision.Allow));
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/guard-home", cancellationToken: ct);
        var events = new List<string>();
        router.Routing += (_, e) => events.Add("routing " + e.Kind);
        router.Routed += (_, e) => events.Add("routed " + e.Outcome);

        var result = await router.GoToAsync("/guard-admin", cancellationToken: ct);

        Assert.True(result.Cancelled);
        Assert.False(result.Success);
        Assert.IsType<GuardHomeVm>(router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        Assert.Equal(["routing Push", "routed Cancelled"], events);
        Assert.True((await router.GoToAsync("/guard-login", cancellationToken: ct)).Success);
    }

    [Fact]
    public async Task A_guard_that_redirects_sends_the_user_to_the_other_route_and_the_refused_page_is_never_entered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(new Guard(c => IsAdmin(c) ? RouteGuardDecision.Redirect("/guard-login") : RouteGuardDecision.Allow));
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/guard-home", cancellationToken: ct);

        var result = await router.GoToAsync("/guard-admin", cancellationToken: ct);

        Assert.True(result.Success);
        Assert.IsType<GuardLoginVm>(router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public async Task A_guard_is_asked_with_the_kind_and_the_address_for_every_route_change()
    {
        var ct = TestContext.Current.CancellationToken;
        var asked = new List<string>();
        using var provider = Build(new Guard(_ => RouteGuardDecision.Allow, asked));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/guard-home", cancellationToken: ct);
        await router.GoToAsync("/guard-login", cancellationToken: ct);
        await router.GoBackAsync(ct);

        Assert.Equal(["Push /guard-home", "Push /guard-login", "Back /guard-home"], asked);
    }

    [Fact]
    public async Task A_redirect_on_back_is_a_cancel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(new Guard(c => c.Kind == RouteableKind.Back ? RouteGuardDecision.Redirect("/guard-admin") : RouteGuardDecision.Allow));
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/guard-home", cancellationToken: ct);
        await router.GoToAsync("/guard-login", cancellationToken: ct);

        var result = await router.GoBackAsync(ct);

        Assert.True(result.Cancelled);
        Assert.IsType<GuardLoginVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Redirects_that_never_end_fail_with_a_message_and_leave_the_router_usable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(new Guard(c => IsAdmin(c) ? RouteGuardDecision.Redirect("/guard-admin") : RouteGuardDecision.Allow));
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/guard-admin", cancellationToken: ct);

        Assert.False(result.Success);
        var error = Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("redirects", error.Message);
        Assert.True((await router.GoToAsync("/guard-home", cancellationToken: ct)).Success);
    }

    [Fact]
    public async Task A_guard_that_throws_fails_the_route_change_and_releases_the_router()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(new ThrowingGuard());
        var router = provider.GetRequiredService<IRouter>();
        var outcomes = new List<RouteableOutcome>();
        router.Routed += (_, e) => outcomes.Add(e.Outcome);

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoToAsync("/guard-home", cancellationToken: ct));

        Assert.Equal([RouteableOutcome.Failed], outcomes);
        // A second call waits for the gate: it would hang here if the first had kept it.
        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoToAsync("/guard-home", cancellationToken: ct));
    }

    [Fact]
    public async Task Guards_are_asked_in_order_and_the_first_refusal_stops_the_others()
    {
        var calls = new List<string>();
        using var provider = Build(
            new Guard(_ => { calls.Add("first"); return RouteGuardDecision.Cancel; }),
            new Guard(_ => { calls.Add("second"); return RouteGuardDecision.Allow; }));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/guard-home", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["first"], calls);
    }

    [Fact]
    public void A_redirect_needs_a_route()
    {
        Assert.Throws<ArgumentException>(() => RouteGuardDecision.Redirect(" "));
        Assert.Equal("/login", RouteGuardDecision.Redirect("/login").RedirectTo);
        Assert.False(RouteGuardDecision.Redirect("/login").IsAllowed);
        Assert.True(RouteGuardDecision.Allow.IsAllowed);
        Assert.False(RouteGuardDecision.Cancel.IsAllowed);
        Assert.Null(RouteGuardDecision.Cancel.RedirectTo);
    }

    // ---- diagnostics ----

    public sealed class MeterHomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MeterHomeVm;

    [Fact]
    public async Task A_route_change_is_reported_as_an_activity_and_a_duration_to_whoever_listens()
    {
        var activities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RouteableDiagnostics.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (activities) activities.Add(activity); }
        };
        ActivitySource.AddActivityListener(activityListener);

        var durations = new List<(double Value, string Kind, string Outcome)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == RouteableDiagnostics.Name)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            var kind = tags.ToArray().Single(t => t.Key == "routeables.kind").Value!.ToString()!;
            var outcome = tags.ToArray().Single(t => t.Key == "routeables.outcome").Value!.ToString()!;
            lock (durations) durations.Add((value, kind, outcome));
        });
        meterListener.Start();

        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<MeterHomeView, MeterHomeVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/meter-home");
        services.AddTransient<MeterHomeVm>();
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IRouter>().GoToAsync("/meter-home", cancellationToken: TestContext.Current.CancellationToken);

        // Other tests route too: only this page counts.
        Activity mine;
        lock (activities) mine = Assert.Single(activities, a => a.GetTagItem("routeables.view_model")?.ToString() == nameof(MeterHomeVm));
        Assert.Equal("Routeables.Route", mine.OperationName);
        Assert.Equal("Push", mine.GetTagItem("routeables.kind"));
        Assert.Equal("/meter-home", mine.GetTagItem("routeables.route"));
        Assert.Equal("Success", mine.GetTagItem("routeables.outcome"));
        Assert.Equal(ActivityStatusCode.Ok, mine.Status);
        lock (durations)
        {
            Assert.Contains(durations, d => d.Kind == "Push" && d.Outcome == "Success" && d.Value >= 0);
        }
    }

    [Fact]
    public async Task A_menu_entry_does_not_remember_the_page_a_guard_sent_the_user_to()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(new Guard(c => IsAdmin(c) ? RouteGuardDecision.Redirect("/guard-login") : RouteGuardDecision.Allow));
        var router = provider.GetRequiredService<IRouter>();
        var entry = new RouteableEntry<GuardAdminVm>("Admin", route: "/guard-admin", template: "/guard-admin");

        var result = await router.GoToRouteAsync(entry, ct);

        Assert.True(result.Success);
        Assert.IsType<GuardLoginVm>(router.CurrentViewModel);
        Assert.Null(entry.View);
        Assert.Null(entry.ViewModel);
    }

    private sealed class OrderedGuard(string name, int order, List<string> asked) : IRouteGuard
    {
        public int Order => order;

        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
        {
            asked.Add(name);
            return Task.FromResult(RouteGuardDecision.Allow);
        }
    }

    [Fact]
    public async Task Guards_are_asked_by_Order_then_by_registration()
    {
        var asked = new List<string>();
        using var provider = Build(
            new OrderedGuard("late", 10, asked),
            new OrderedGuard("first registered", 0, asked),
            new OrderedGuard("early", -5, asked),
            new OrderedGuard("second registered", 0, asked));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/guard-home", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "early", "first registered", "second registered", "late" }, asked);
    }

    [Fact]
    public async Task The_same_guards_are_asked_on_every_route_change()
    {
        var asked = new List<string>();
        using var provider = Build(new OrderedGuard("g", 0, asked));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/guard-home", cancellationToken: TestContext.Current.CancellationToken);
        await router.GoToAsync("/guard-admin", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "g", "g" }, asked);
    }
}
