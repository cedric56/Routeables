using Bunit;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor.Components;
using Routeables.Routing;

namespace Routeables.Tests.Razor;

/// <summary>A click on <see cref="RouteableLink"/>: it goes through the router, unless the link opens elsewhere, and what goes wrong is reported (rendered with bUnit, which can click).</summary>
public sealed class LinkInteractionTests : BunitContext
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Exception)> Reported { get; } = [];
        public void Report(string source, Exception exception) => Reported.Add((source, exception));
    }

    private sealed class NullGuard : IRouteGuard
    {
        // A guard that answers nothing: the router refuses it with an exception instead of a result.
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult<RouteGuardDecision>(null!);
    }

    private readonly Sink _sink = new();

    // The router and the sink change without a render of the link, which is all that WaitForAssertion looks at: this one just asks again.
    private static async Task UntilAsync(Action assertion)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try { assertion(); return; }
            catch (Xunit.Sdk.XunitException) when (DateTime.UtcNow < deadline) { await Task.Delay(10, Xunit.TestContext.Current.CancellationToken); }
        }
    }

    // What the link reported, to read in the failure when the router did not move.
    private string Reports() => "The router did not go to the page. Reported: " + string.Join(" | ", _sink.Reported.Select(r => $"{r.Source}: {r.Exception}"));

    // bUnit resolves from the root provider, which refuses a scoped service: what the test registered as scoped is one per root here.
    private void SingletonsFrom(int first)
    {
        for (var i = first; i < Services.Count; i++)
        {
            var descriptor = Services[i];
            if (descriptor.Lifetime != ServiceLifetime.Scoped || descriptor.IsKeyedService)
                continue;
            Services[i] = descriptor.ImplementationFactory is { } factory ? new ServiceDescriptor(descriptor.ServiceType, factory, ServiceLifetime.Singleton)
                : descriptor.ImplementationType is { } type ? new ServiceDescriptor(descriptor.ServiceType, type, ServiceLifetime.Singleton)
                : descriptor;
        }
    }

    private IRouter Start(bool guarded = false)
    {
        Services.AddSingleton<IRouteableErrorSink>(_sink);
        var before = Services.Count;
        Services.RegisterRouting();
        Services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        Services.RegisterRoute<RouteableLinkTests.OrderView, RouteableLinkTests.OrderVm>(route: "/orders/{id:int}");
        if (guarded)
            Services.AddSingleton<IRouteGuard, NullGuard>();
        SingletonsFrom(before);
        return Services.GetRequiredService<IRouter>();
    }

    private IRenderedComponent<RouteableLink> Link(string route, string? target = null)
        => Render<RouteableLink>(parameters =>
        {
            parameters.Add(p => p.Route, route);
            if (target is not null)
                parameters.AddUnmatched("target", target);
        });

    [Fact]
    public async Task A_click_on_the_link_goes_to_its_route_through_the_router()
    {
        var router = Start();
        var cut = Link("/orders/42");

        cut.Find("a").Click();

        await UntilAsync(() => Assert.True(router.CurrentViewModel is RouteableLinkTests.OrderVm { Id: 42 }, Reports()));
    }

    [Theory]
    [InlineData("_blank")]
    [InlineData("_top")]
    public async Task A_link_that_opens_elsewhere_is_left_to_the_browser(string target)
    {
        var router = Start();
        var cut = Link("/orders/42", target);

        cut.Find("a").Click();

        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.Null(router.CurrentViewModel);
        Assert.Equal(target, cut.Find("a").GetAttribute("target"));
    }

    [Fact]
    public async Task A_link_with_the_target_self_goes_through_the_router()
    {
        var router = Start();
        var cut = Link("/orders/7", "_SELF");

        cut.Find("a").Click();

        await UntilAsync(() => Assert.True(router.CurrentViewModel is RouteableLinkTests.OrderVm { Id: 7 }, Reports()));
    }

    [Fact]
    public async Task A_click_on_a_link_with_no_route_does_nothing()
    {
        var router = Start();
        var cut = Link("  ");

        cut.Find("a").Click();

        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.Null(router.CurrentViewModel);
        Assert.Empty(_sink.Reported);
    }

    [Fact]
    public async Task A_route_that_cannot_be_reached_is_reported_with_the_name_of_the_link()
    {
        Start();
        var cut = Link("/nowhere");

        cut.Find("a").Click();

        await UntilAsync(() => Assert.Contains(_sink.Reported, r => r.Source == "RouteableLink"));
    }

    [Fact]
    public async Task A_router_that_throws_is_reported_and_the_click_does_not_fail()
    {
        var router = Start(guarded: true);
        var cut = Link("/orders/42");

        cut.Find("a").Click();

        await UntilAsync(() =>
        {
            var reported = Assert.Single(_sink.Reported, r => r.Source == "RouteableLink");
            Assert.Contains("returned null", reported.Exception.Message);
        });
        Assert.Null(router.CurrentViewModel);
    }
}
