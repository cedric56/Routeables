using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Tests.Core;
using System.Diagnostics;
using Routeables.Regions;
using Routeables.Locking;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// The application's callbacks run from event handlers of the UI, where an exception would end the program: <c>Guard</c> reports it
/// like every other swallowed error. The errors that used to vanish (a region that cannot follow, a failed recovery, a failed
/// selection) are reported too.
/// </summary>
public class RouteableErrorsGuardTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Exception)> Reported { get; } = [];
        public void Report(string source, Exception exception) => Reported.Add((source, exception));
    }

    private sealed class Capture : TraceListener
    {
        public List<string> Lines { get; } = [];
        public override void Write(string? message) { lock (Lines) Lines.Add(message ?? ""); }
        public override void WriteLine(string? message) { lock (Lines) Lines.Add(message ?? ""); }
        public bool Saw(string text) { lock (Lines) return Lines.Any(l => l.Contains(text)); }
    }

    private sealed class Owner<T>
    {
        public static void Fail(IServiceProvider provider, string? detail = null)
            => RouteableErrors.Report<Owner<T>>(provider, new InvalidOperationException("boom"), detail);
    }

    [Fact]
    public void The_source_of_a_report_is_the_owner_and_the_method_that_caught_the_error()
    {
        var (provider, sink) = WithSink();

        Owner<int>.Fail(provider);
        Owner<int>.Fail(provider, "/orders");
        RouteableErrors.Report(provider, new InvalidOperationException("boom"), typeof(RouteableErrors), "detail");

        Assert.Equal(
            ["Owner.Fail", "Owner.Fail '/orders'", $"{nameof(RouteableErrors)}.{nameof(The_source_of_a_report_is_the_owner_and_the_method_that_caught_the_error)} 'detail'"],
            sink.Reported.Select(r => r.Source));
    }

    private static (ServiceProvider Provider, Sink Sink) WithSink()
    {
        var sink = new Sink();
        return (new ServiceCollection().AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider(), sink);
    }

    // Trace is process-wide: each test looks for text of its own.
    private static T Traced<T>(Capture capture, Func<T> run)
    {
        Trace.Listeners.Add(capture);
        try { return run(); }
        finally { Trace.Listeners.Remove(capture); }
    }

    [Fact]
    public void Guard_runs_the_callback_and_ignores_a_missing_one()
    {
        var ran = 0;
        RouteableErrors.Guard(null, "test", () => ran++);
        RouteableErrors.Guard(null, "test", (Action?)null);
        RouteableErrors.Guard(null, "test", (Action<int>?)null, 1);

        Assert.Equal(1, ran);
    }

    [Fact]
    public void Guard_reports_a_callback_that_throws_instead_of_propagating_it()
    {
        var (provider, sink) = WithSink();
        var boom = new InvalidOperationException("boom");

        RouteableErrors.Guard(provider, "Dialog.Opened", () => throw boom);
        RouteableErrors.Guard<int>(provider, "Dialog.Arg", _ => throw boom, 5);

        Assert.Equal(["Dialog.Opened", "Dialog.Arg"], sink.Reported.Select(r => r.Source));
        Assert.All(sink.Reported, r => Assert.Same(boom, r.Exception));
    }

    [Fact]
    public void Guard_passes_the_argument_and_answers_with_the_callback_or_the_fallback()
    {
        var (provider, sink) = WithSink();
        string? seen = null;

        RouteableErrors.Guard(provider, "test", (string s) => seen = s, "x");
        Assert.Equal("x", seen);

        Assert.True(RouteableErrors.Guard(provider, "test", () => true));
        Assert.True(RouteableErrors.Guard(provider, "test", (string s) => s == "x", "x"));
        Assert.False(RouteableErrors.Guard(provider, "test", (Func<bool>?)null));
        Assert.True(RouteableErrors.Guard(provider, "test", (Func<bool>?)null, fallback: true));
        Assert.Empty(sink.Reported);

        // A question that cannot be answered is answered with the fallback: Closing => false lets the dialog close.
        Assert.False(RouteableErrors.Guard(provider, "Dialog.Closing", () => throw new InvalidOperationException("no")));
        Assert.True(RouteableErrors.Guard(provider, "Dialog.Closing", (string _) => throw new InvalidOperationException("no"), "x", fallback: true));
        Assert.Equal(2, sink.Reported.Count);
    }

    [Fact]
    public async Task A_region_that_cannot_follow_is_reported_when_nobody_listens_to_Failed()
    {
        var capture = new Capture();
        using var provider = RegionTestServices.Build(s => s.AddRegion("content"));
        using var follower = new RegionFollower(provider.GetRouter("content"), () => throw new InvalidOperationException("follow-me-not-1"));

        Trace.Listeners.Add(capture);
        try
        {
            follower.Update();
            await follower.SettledAsync();
        }
        finally { Trace.Listeners.Remove(capture); }

        Assert.True(capture.Saw("follow-me-not-1"));
    }

    [Fact]
    public async Task A_Failed_handler_that_throws_neither_stops_the_updates_nor_goes_unnoticed()
    {
        var capture = new Capture();
        using var provider = RegionTestServices.Build(s => s.AddRegion("content"));
        var runs = 0;
        using var follower = new RegionFollower(provider.GetRouter("content"), () =>
        {
            runs++;
            throw new InvalidOperationException("update-failed");
        });
        follower.Failed += (_, _) => throw new InvalidOperationException("handler-failed-2");

        Trace.Listeners.Add(capture);
        try
        {
            follower.Update();
            follower.Update();
            await follower.SettledAsync();
        }
        finally { Trace.Listeners.Remove(capture); }

        Assert.Equal(2, runs);
        Assert.True(capture.Saw("handler-failed-2"));
    }

    [Fact]
    public void A_failed_selection_is_reported_by_default()
    {
        var capture = new Capture();
        var services = new ServiceCollection();
        services.AddLocking();
        var router = new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), services.BuildServiceProvider());
        var sut = TestNavigableViewModel.Create(router, [], initialize: false);
        var method = typeof(RouteableViewModelBase).GetMethod("OnSelectionFailed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        Traced(capture, () => method.Invoke(sut, [new InvalidOperationException("selection-failed-3")]));

        Assert.True(capture.Saw("selection-failed-3"));
    }

    // RouteableErrors.Default is process-wide: these tests restore it, and look only at errors with a source of their own.
    [Fact]
    public void Default_receives_what_is_reported_without_a_container_and_guarded_callbacks()
    {
        var sink = new Sink();
        RouteableErrors.Default = sink;
        try
        {
            RouteableErrors.Report(null, "default-test-1", new InvalidOperationException("a"));
            RouteableErrors.Guard(null, "default-test-2", () => throw new InvalidOperationException("b"));
            RouteableErrors.Report(new ServiceCollection().BuildServiceProvider(), "default-test-3", new InvalidOperationException("c"));
        }
        finally { RouteableErrors.Default = null; }

        Assert.Equal(["default-test-1", "default-test-2", "default-test-3"],
            sink.Reported.Select(r => r.Source).Where(s => s.StartsWith("default-test-")));
    }

    [Fact]
    public void A_sink_of_the_container_wins_over_Default()
    {
        var (provider, registered) = WithSink();
        var fallback = new Sink();
        RouteableErrors.Default = fallback;
        try { RouteableErrors.Report(provider, "default-test-4", new InvalidOperationException("d")); }
        finally { RouteableErrors.Default = null; }

        Assert.Contains(registered.Reported, r => r.Source == "default-test-4");
        Assert.DoesNotContain(fallback.Reported, r => r.Source == "default-test-4");
    }

    [Fact]
    public void A_Default_that_throws_never_fails_the_reporting()
    {
        RouteableErrors.Default = new ThrowingSink();
        try { RouteableErrors.Report(null, "default-test-5", new InvalidOperationException("e")); }
        finally { RouteableErrors.Default = null; }
    }

    private sealed class ThrowingSink : IRouteableErrorSink
    {
        public void Report(string source, Exception exception) => throw new InvalidOperationException("sink failed");
    }
}
