using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Registrations;
using Routeables.Regions;
using Routeables.Routing;

namespace Routeables.Tests.Regions;

/// <summary>The start of a region that goes wrong: it is called from an event of the interface, so it reports and does not throw.</summary>
public class RegionStartTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        private readonly object gate = new();
        private readonly List<(string Source, Exception Exception)> reported = [];

        public (string Source, Exception Exception)[] Reported { get { lock (gate) return [.. reported]; } }

        public void Report(string source, Exception exception) { lock (gate) reported.Add((source, exception)); }
    }

    private static async Task<(string Source, Exception Exception)> FirstReportAsync(Sink sink)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < until && sink.Reported.Length == 0)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.NotEmpty(sink.Reported);
        return sink.Reported[0];
    }

    [Fact]
    public async Task A_start_route_that_cannot_be_opened_is_reported()
    {
        var sink = new Sink();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddSingleton<IRouteableErrorSink>(sink);
        using var provider = services.BuildServiceProvider();

        RegionStart.Begin(provider, null, "/rs-no-such-page");

        var report = await FirstReportAsync(sink);
        Assert.NotNull(report.Exception);
    }

    [Fact]
    public async Task A_container_with_no_router_is_reported_and_does_not_throw()
    {
        var sink = new Sink();
        using var provider = new ServiceCollection().AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider();

        RegionStart.Begin(provider, null, "/rs-home");

        var report = await FirstReportAsync(sink);
        Assert.IsType<InvalidOperationException>(report.Exception);
    }
}
