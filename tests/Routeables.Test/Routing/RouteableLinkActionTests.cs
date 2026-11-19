using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Routing;

/// <summary>What every host's RouteableLink does when its button is clicked.</summary>
// RouteableErrors.Default is process-wide: the classes that replace it, or read what goes to it, run one after the other.
[Collection("RouteableErrors.Default")]
public class RouteableLinkActionTests
{

    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Error)> Reports { get; } = [];
        public void Report(string source, Exception exception) => Reports.Add((source, exception));
    }

    private static ServiceProvider Build(Sink sink)
    {
        var services = new ServiceCollection();
        services.AddRoutes(n => n.Page<FakeViewFor, FakeViewModel>("/hello"));
        services.AddSingleton<IRouteableErrorSink>(sink);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_click_goes_to_the_route_without_a_report()
    {
        var sink = new Sink();
        using var provider = Build(sink);

        await RouteableLinkAction.GoAsync(provider, "/hello", null);

        Assert.IsType<FakeViewModel>(provider.GetRequiredService<IRouter>().CurrentViewModel);
        Assert.Empty(sink.Reports);
    }

    [Fact]
    public async Task A_route_that_fails_is_reported_and_does_not_throw()
    {
        var sink = new Sink();
        using var provider = Build(sink);

        await RouteableLinkAction.GoAsync(provider, "/unknown", null);

        var report = Assert.Single(sink.Reports);
        Assert.Equal("RouteableLink", report.Source);
    }

    [Fact]
    public async Task A_host_that_is_not_started_is_reported_and_does_not_throw()
    {
        var sink = new Sink();
        var previous = RouteableErrors.Default;
        RouteableErrors.Default = sink;
        try
        {
            await RouteableLinkAction.GoAsync(null, "/hello", null);
        }
        finally { RouteableErrors.Default = previous; }

        // Other tests in the process may report to the same default meanwhile: only the report of the link counts.
        Assert.Contains("UseRouteables", Assert.Single(sink.Reports, r => r.Source == "RouteableLink").Error.Message);
    }
}
