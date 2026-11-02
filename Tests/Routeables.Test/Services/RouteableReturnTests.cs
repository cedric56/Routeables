using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Services;

/// <summary><see cref="RouteableReturn"/>: the route a guard turned away, remembered until the sign-in page takes the user back to it.</summary>
public class RouteableReturnTests
{
    private static RouteableRoutingEventArgs Change(RouteableKind kind, string? route)
    {
        var to = Substitute.For<IHistoryEntry>();
        to.Args.Returns(RouteableArgs.Create().WithRoute(route));
        return new RouteableRoutingEventArgs(kind, null, to);
    }

    [Fact]
    public void Nothing_is_remembered_at_first()
    {
        var returns = new RouteableReturn();

        Assert.Null(returns.Pending);
        Assert.Null(returns.Take());
    }

    [Fact]
    public void Remember_then_Take_gives_the_route_once()
    {
        var returns = new RouteableReturn();
        returns.Remember("/orders/42");

        Assert.Equal("/orders/42", returns.Pending);
        Assert.Equal("/orders/42", returns.Take());
        Assert.Null(returns.Pending);
        Assert.Null(returns.Take());
    }

    [Fact]
    public void Pending_does_not_forget_the_route()
    {
        var returns = new RouteableReturn();
        returns.Remember("/a");

        _ = returns.Pending;
        _ = returns.Pending;

        Assert.Equal("/a", returns.Take());
    }

    [Fact]
    public void A_route_replaces_the_one_remembered_before()
    {
        var returns = new RouteableReturn();
        returns.Remember("/first");
        returns.Remember("/second");

        Assert.Equal("/second", returns.Take());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_route_forgets_what_was_remembered(string? blank)
    {
        var returns = new RouteableReturn();
        returns.Remember("/first");

        returns.Remember(blank);

        Assert.Null(returns.Pending);
    }

    [Fact]
    public void A_push_is_remembered_and_redirected()
    {
        var returns = new RouteableReturn();

        var decision = returns.RedirectFrom(Change(RouteableKind.Push, "/orders/42"), "/sign-in");

        Assert.False(decision.IsAllowed);
        Assert.Equal("/sign-in", decision.RedirectTo);
        Assert.Equal("/orders/42", returns.Pending);
    }

    [Theory]
    [InlineData(RouteableKind.Back)]
    [InlineData(RouteableKind.Forward)]
    [InlineData(RouteableKind.Pop)]
    [InlineData(RouteableKind.Root)]
    public void Only_a_push_is_remembered(RouteableKind kind)
    {
        var returns = new RouteableReturn();

        var decision = returns.RedirectFrom(Change(kind, "/orders/42"), "/sign-in");

        Assert.Equal("/sign-in", decision.RedirectTo);
        Assert.Null(returns.Pending);
    }

    [Fact]
    public void A_push_without_a_route_of_its_own_forgets_what_was_remembered()
    {
        var returns = new RouteableReturn();
        returns.Remember("/old");

        returns.RedirectFrom(Change(RouteableKind.Push, null), "/sign-in");

        Assert.Null(returns.Pending);
    }

    [Fact]
    public void RedirectFrom_refuses_no_change_and_no_route()
    {
        var returns = new RouteableReturn();

        Assert.Throws<ArgumentNullException>(() => returns.RedirectFrom(null!, "/sign-in"));
        Assert.ThrowsAny<ArgumentException>(() => returns.RedirectFrom(Change(RouteableKind.Push, "/a"), " "));
    }

    [Fact]
    public async Task Concurrent_remembers_and_takes_never_give_a_route_twice()
    {
        var returns = new RouteableReturn();
        var taken = 0;
        returns.Remember("/once");

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            if (returns.Take() is not null) Interlocked.Increment(ref taken);
        })));

        Assert.Equal(1, taken);
    }

    [Fact]
    public void AddRouteableReturn_registers_one_for_the_application_and_can_be_called_twice()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddRouteableReturn());
        services.AddRouteableReturn();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(RouteableReturn)));
        Assert.Same(provider.GetRequiredService<RouteableReturn>(), provider.GetRequiredService<RouteableReturn>());
    }

    [Fact]
    public void AddRouteableReturn_keeps_a_registration_made_before()
    {
        var mine = new RouteableReturn();
        var services = new ServiceCollection();
        services.AddSingleton(mine);

        services.AddRouteableReturn();
        using var provider = services.BuildServiceProvider();

        Assert.Same(mine, provider.GetRequiredService<RouteableReturn>());
    }

    [Fact]
    public void AddRouteableReturn_refuses_no_services()
        => Assert.Throws<ArgumentNullException>(() => RouteableReturnExtensions.AddRouteableReturn(null!));
}
