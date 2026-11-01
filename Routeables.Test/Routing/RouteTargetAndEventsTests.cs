using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>What a host does with a control of navigation (<see cref="RouteTarget"/>) and how it tells the observers of a route change (<see cref="RouteableEvents"/>).</summary>
public class RouteTargetAndEventsTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Exception)> Reported { get; } = [];
        public void Report(string source, Exception exception) => Reported.Add((source, exception));
    }

    private static IRouter RouterShowing(string? route)
    {
        var router = Substitute.For<IRouter>();
        router.History.Current!.Args.Route.Returns(route);
        return router;
    }

    private static ServiceProvider ProviderWith(Sink sink)
        => new ServiceCollection().AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider();

    // ------------------------------------------------------------------------------------------------ RouteTarget

    [Theory]
    [InlineData("/orders", "orders", true)]
    [InlineData("orders/", "/Orders", true)]
    [InlineData("/orders", "/invoices", false)]
    [InlineData(null, "", true)]
    [InlineData(null, "orders", false)]
    public void A_route_is_current_whatever_its_slashes_and_case(string? shown, string? asked, bool expected)
    {
        Assert.Equal(expected, RouteTarget.IsCurrent(RouterShowing(shown), asked));
    }

    [Fact]
    public void IsCurrent_refuses_a_null_router()
    {
        Assert.Throws<ArgumentNullException>(() => RouteTarget.IsCurrent(null!, "/orders"));
    }

    [Fact]
    public async Task A_route_the_router_does_not_show_is_routed_with_a_leading_slash()
    {
        var router = RouterShowing("/home");
        using var provider = ProviderWith(new Sink());

        await RouteTarget.GoAsync(provider, router, "orders/", typeof(RouteTargetAndEventsTests));

        await router.Received(1).GoToAsync("/orders", Arg.Any<IRouteableArgs?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_route_the_router_already_shows_is_not_routed_twice()
    {
        var router = RouterShowing("/orders");
        using var provider = ProviderWith(new Sink());

        await RouteTarget.GoAsync(provider, router, "/Orders", typeof(RouteTargetAndEventsTests));

        await router.DidNotReceiveWithAnyArgs().GoToAsync(default!, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_error_while_routing_is_reported_to_the_sink_and_not_thrown()
    {
        var router = RouterShowing("/home");
        router.GoToAsync(Arg.Any<string>(), Arg.Any<IRouteableArgs?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no way"));
        var sink = new Sink();
        using var provider = ProviderWith(sink);

        await RouteTarget.GoAsync(provider, router, "/orders", typeof(RouteTargetAndEventsTests));

        var reported = Assert.Single(sink.Reported);
        Assert.IsType<InvalidOperationException>(reported.Exception);
        Assert.Contains(nameof(RouteTargetAndEventsTests), reported.Source);
    }

    // ------------------------------------------------------------------------------------------------ RouteableEvents

    [Fact]
    public void Without_observers_nothing_happens()
    {
        using var provider = ProviderWith(new Sink());

        RouteableEvents.Raise<EventArgs>(provider, typeof(RouteTargetAndEventsTests), null, this, EventArgs.Empty);
    }

    [Fact]
    public void Every_observer_is_called_with_the_sender_and_the_arguments_in_order()
    {
        using var provider = ProviderWith(new Sink());
        var calls = new List<string>();
        var args = new EventArgs();
        EventHandler<EventArgs>? handlers = null;
        handlers += (s, e) => { Assert.Same(this, s); Assert.Same(args, e); calls.Add("first"); };
        handlers += (_, _) => calls.Add("second");

        RouteableEvents.Raise(provider, typeof(RouteTargetAndEventsTests), handlers, this, args);

        Assert.Equal(["first", "second"], calls);
    }

    [Fact]
    public void An_observer_that_throws_is_reported_and_the_others_are_still_called()
    {
        var sink = new Sink();
        using var provider = ProviderWith(sink);
        var calls = new List<string>();
        EventHandler<EventArgs>? handlers = null;
        handlers += (_, _) => calls.Add("before");
        handlers += (_, _) => throw new InvalidOperationException("observer");
        handlers += (_, _) => calls.Add("after");

        RouteableEvents.Raise(provider, typeof(RouteTargetAndEventsTests), handlers, this, EventArgs.Empty);

        Assert.Equal(["before", "after"], calls);
        var reported = Assert.Single(sink.Reported);
        Assert.Equal("observer", reported.Exception.Message);
        Assert.Contains(nameof(EventArgs), reported.Source);
    }
}
