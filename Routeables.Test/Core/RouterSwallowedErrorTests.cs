using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>Errors that must not fail a routing are still reported, to the registered sink.</summary>
public class RouterSwallowedErrorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Exception)> Reported { get; } = [];
        public void Report(string source, Exception exception) => Reported.Add((source, exception));
    }

    private sealed class ThrowingDispose : IRouteable, IDisposable
    {
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() => throw new InvalidOperationException("dispose failed");
    }

    private static (IRouter Router, Sink? Sink) Create(bool withSink)
    {
        var sink = withSink ? new Sink() : null;
        var services = new ServiceCollection();
        if (sink is not null)
            services.AddSingleton<IRouteableErrorSink>(sink);
        var provider = services.BuildServiceProvider();
        return (new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(provider), provider), sink);
    }

    [Fact]
    public async Task A_handler_that_throws_is_reported_and_the_routing_still_succeeds()
    {
        var (router, sink) = Create(withSink: true);
        router.Routed += (_, _) => throw new InvalidOperationException("handler failed");

        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct);

        Assert.True(result.Success);
        Assert.Single(sink!.Reported);
        Assert.Contains("Router", sink.Reported[0].Source);
        Assert.Equal("handler failed", sink.Reported[0].Exception.Message);
    }

    [Fact]
    public async Task A_dispose_that_throws_when_an_entry_is_popped_is_reported_and_the_pop_succeeds()
    {
        var (router, sink) = Create(withSink: true);
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct);
        await router.GoAsync(new TestView(), new ThrowingDispose(), cancellationToken: Ct);

        var result = await router.PopAsync(Ct);

        Assert.True(result.Success);
        Assert.Single(sink!.Reported);
        Assert.Equal("RouterLifecycle.Destroy", sink.Reported[0].Source);
    }

    [Fact]
    public async Task Without_a_sink_the_error_is_still_swallowed_without_failing_the_routing()
    {
        var (router, _) = Create(withSink: false);
        router.Routed += (_, _) => throw new InvalidOperationException("handler failed");

        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct);

        Assert.True(result.Success);
    }

    [Fact]
    public void A_sink_that_throws_does_not_become_a_new_error()
    {
        var services = new ServiceCollection().AddSingleton<IRouteableErrorSink, FailingSink>().BuildServiceProvider();

        RouteableErrors.Report(services, "test", new InvalidOperationException("first"));
    }

    private sealed class FailingSink : IRouteableErrorSink
    {
        public void Report(string source, Exception exception) => throw new InvalidOperationException("sink failed");
    }
}
