using Routeables.Abstraction.Errors;
using Routeables.AspNetCore;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

/// <summary>The handlers of a toast belong to the application: one that throws is reported and does not reach the call that happened to run it.</summary>
// RouteableErrors.Default is process-wide: the classes that replace it run one after the other.
[Collection("RouteableErrors.Default")]
public class AspNetCoreToastHandlerTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Error)> Reports { get; } = [];
        public void Report(string source, Exception exception) => Reports.Add((source, exception));
    }

    [Fact]
    public void A_click_handler_that_throws_is_reported_and_the_toast_still_closes()
    {
        var sink = new Sink();
        var previous = RouteableErrors.Default;
        RouteableErrors.Default = sink;
        try
        {
            using var interactions = new RouteableInteractions();
            var closed = false;
            interactions.Notify("T", "M", RouteableNotificationType.Information, null, () => throw new InvalidOperationException("click"), () => closed = true);

            interactions.Dismiss(interactions.Toasts[0].Id, click: true);

            Assert.True(closed);
            Assert.Empty(interactions.Toasts);
        }
        finally { RouteableErrors.Default = previous; }

        Assert.Contains(sink.Reports, r => r.Error.Message == "click");
    }

    [Fact]
    public void A_close_handler_that_throws_on_an_evicted_toast_does_not_fail_the_call_that_evicted_it()
    {
        var sink = new Sink();
        var previous = RouteableErrors.Default;
        RouteableErrors.Default = sink;
        try
        {
            using var interactions = new RouteableInteractions();
            interactions.Notify("first", "m", RouteableNotificationType.Information, null, null, () => throw new InvalidOperationException("close"));
            for (var i = 1; i < RouteableInteractions.MaxToasts; i++)
                interactions.Notify("n", "m", RouteableNotificationType.Information, null, null, null);
            // One more than the limit: the first, whose handler throws, is evicted by this call, which must not fail.
            interactions.Notify("last", "m", RouteableNotificationType.Information, null, null, null);

            Assert.Equal(RouteableInteractions.MaxToasts, interactions.Toasts.Count);
        }
        finally { RouteableErrors.Default = previous; }

        Assert.Contains(sink.Reports, r => r.Error.Message == "close");
    }
}
