using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary><see cref="IRouteable.OnSubscribe"/> once per instance before its first entering, <see cref="IRouteable.OnUnsubscribe"/> when the router disposes it.</summary>
public class RouterSubscribeTests
{
    private sealed class Probe(string name, List<string> calls, bool failOnUnsubscribe = false) : IRouteable, IDisposable
    {
        public bool FailOnSubscribe { get; set; }

        public void OnSubscribe()
        {
            calls.Add($"{name}:subscribe");
            if (FailOnSubscribe)
                throw new InvalidOperationException("subscribe failed");
        }

        public void OnUnsubscribe()
        {
            calls.Add($"{name}:unsubscribe");
            if (failOnUnsubscribe)
                throw new InvalidOperationException("unsubscribe failed");
        }

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            calls.Add($"{name}:to");
            return Task.CompletedTask;
        }

        public void Dispose() => calls.Add($"{name}:dispose");
    }

    private static RouterEntry Entry(object viewModel, object view) => new(viewModel, view, RouteableArgs.Create());

    [Fact]
    public async Task OnSubscribe_is_called_once_per_instance_before_the_first_entering()
    {
        var calls = new List<string>();
        var lifecycle = new RouterLifecycle();
        var page = Entry(new Probe("vm", calls), new Probe("view", calls));
        var other = Entry(new Probe("other-vm", calls), new Probe("other-view", calls));

        await lifecycle.EnterAsync(page, TestContext.Current.CancellationToken);
        await lifecycle.EnterAsync(other, TestContext.Current.CancellationToken);
        await lifecycle.EnterAsync(page, TestContext.Current.CancellationToken);   // back to the page: entering again, subscribed once

        Assert.Equal(
            ["vm:subscribe", "view:subscribe", "vm:to", "view:to",
             "other-vm:subscribe", "other-view:subscribe", "other-vm:to", "other-view:to",
             "vm:to", "view:to"],
            calls);
    }

    [Fact]
    public async Task An_instance_that_is_both_view_and_view_model_is_subscribed_once()
    {
        var calls = new List<string>();
        var both = new Probe("both", calls);

        await new RouterLifecycle().EnterAsync(Entry(both, both), TestContext.Current.CancellationToken);

        Assert.Single(calls, c => c == "both:subscribe");
    }

    [Fact]
    public async Task OnUnsubscribe_is_called_before_the_router_disposes_a_page_that_left_the_history()
    {
        var calls = new List<string>();
        var lifecycle = new RouterLifecycle();
        var page = Entry(new Probe("vm", calls), new Probe("view", calls));
        await lifecycle.EnterAsync(page, TestContext.Current.CancellationToken);
        calls.Clear();

        await lifecycle.DestroyAsync([page], []);

        Assert.Equal(["vm:unsubscribe", "vm:dispose", "view:unsubscribe", "view:dispose"], calls);
    }

    [Fact]
    public async Task A_page_two_navigations_discard_is_disposed_once()
    {
        var calls = new List<string>();
        var lifecycle = new RouterLifecycle();
        var page = Entry(new Probe("vm", calls), new Probe("view", calls));

        await lifecycle.DestroyAsync([page], []);
        await lifecycle.DestroyAsync([page], []);

        Assert.Equal(["vm:dispose", "view:dispose"], calls);
    }

    [Fact]
    public async Task A_page_that_was_never_entered_is_not_unsubscribed()
    {
        var calls = new List<string>();

        await new RouterLifecycle().DestroyAsync([Entry(new Probe("vm", calls), new Probe("view", calls))], []);

        Assert.Equal(["vm:dispose", "view:dispose"], calls);
    }

    [Fact]
    public async Task A_failing_OnUnsubscribe_does_not_prevent_the_dispose()
    {
        var calls = new List<string>();
        var lifecycle = new RouterLifecycle();
        var page = Entry(new Probe("vm", calls, failOnUnsubscribe: true), new Probe("view", calls));
        await lifecycle.EnterAsync(page, TestContext.Current.CancellationToken);
        calls.Clear();

        await lifecycle.DestroyAsync([page], []);

        Assert.Contains("vm:dispose", calls);
        Assert.Contains("view:dispose", calls);
    }

    [Fact]
    public async Task An_instance_whose_OnSubscribe_failed_subscribes_again_the_next_time_it_is_entered()
    {
        var calls = new List<string>();
        var lifecycle = new RouterLifecycle();
        var vm = new Probe("vm", calls) { FailOnSubscribe = true };
        var page = Entry(vm, new Probe("view", calls));

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.EnterAsync(page, TestContext.Current.CancellationToken));
        Assert.Equal(["vm:subscribe"], calls);          // the failure stopped the entering: no OnEntering, nothing for the view

        // A page kept in a cache is entered again by the next try: it has not subscribed, so it does.
        vm.FailOnSubscribe = false;
        calls.Clear();
        await lifecycle.EnterAsync(page, TestContext.Current.CancellationToken);
        Assert.Equal(["vm:subscribe", "view:subscribe", "vm:to", "view:to"], calls);

        // And once subscribed, it does not subscribe again.
        calls.Clear();
        await lifecycle.EnterAsync(page, TestContext.Current.CancellationToken);
        Assert.Equal(["vm:to", "view:to"], calls);
    }

    [Fact]
    public async Task An_instance_whose_OnSubscribe_failed_is_not_unsubscribed_when_it_goes()
    {
        var calls = new List<string>();
        var lifecycle = new RouterLifecycle();
        var page = Entry(new Probe("vm", calls) { FailOnSubscribe = true }, new Probe("view", calls));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.EnterAsync(page, TestContext.Current.CancellationToken));
        calls.Clear();

        await lifecycle.DestroyAsync([page], []);

        Assert.DoesNotContain("vm:unsubscribe", calls);
        Assert.Contains("vm:dispose", calls);
    }

    public sealed class CountedView : IRouteableFor { public object? Context { get; set; } }
    public sealed class CountedVm : IDisposable
    {
        public static int Disposed;
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    public sealed class OrderedView : IRouteableFor { public object? Context { get; set; } }

    public sealed class OrderedVm : IRouteable, IDisposable
    {
        public static List<string> Log { get; } = [];
        public void OnUnsubscribe() => Log.Add("unsubscribe");
        public Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default) { Log.Add("left"); return Task.CompletedTask; }
        public void Dispose() => Log.Add("dispose");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task EraseHistory_tells_the_page_it_is_left_before_it_is_disposed()
    {
        OrderedVm.Log.Clear();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<OrderedView, OrderedVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ordered");
        services.AddTransient<OrderedVm>();
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/ordered", cancellationToken: Ct);

        await router.EraseHistoryAsync(Ct);

        var left = OrderedVm.Log.IndexOf("left");
        Assert.True(left >= 0, "OnLeftAsync was not called");
        Assert.True(left < OrderedVm.Log.IndexOf("dispose"), string.Join(",", OrderedVm.Log));
        Assert.True(left < OrderedVm.Log.IndexOf("unsubscribe"), string.Join(",", OrderedVm.Log));
    }
}
