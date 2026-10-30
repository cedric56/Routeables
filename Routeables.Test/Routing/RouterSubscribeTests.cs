using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary><see cref="IRouteable.OnSubscribe"/> once per instance before its first entering, <see cref="IRouteable.OnUnsubscribe"/> when the router disposes it.</summary>
public class RouterSubscribeTests
{
    private sealed class Probe(string name, List<string> calls, bool failOnUnsubscribe = false) : IRouteable, IDisposable
    {
        public void OnSubscribe() => calls.Add($"{name}:subscribe");

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
}
