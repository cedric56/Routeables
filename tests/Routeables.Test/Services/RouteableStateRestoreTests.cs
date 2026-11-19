using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>Restoring the history from the saved routes: what a guard that redirects does to it, and what a command (<c>-/orders</c>, <c>-</c>) leaves of it.</summary>
public class RouteableStateRestoreTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrderVm(int id) { public int Id { get; } = id; }

    // Nobody is signed in: the pages of the orders send to the sign-in page.
    public sealed class SignInGuard : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(change.To.ViewModel is OrdersVm or OrderVm ? RouteGuardDecision.Redirect("/login") : RouteGuardDecision.Allow);
    }

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<LoginView, LoginVm>(route: "/login");
        services.RegisterRoute<OrdersView, OrdersVm>(route: "/orders");
        services.RegisterRoute<OrderView, OrderVm>(route: "/orders/{id:int}");
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_restoration_whose_pages_a_guard_redirects_does_not_pile_up_the_redirections()
    {
        await using var provider = Build(services => services.AddSingleton<IRouteGuard, SignInGuard>());
        var router = provider.GetRequiredService<IRouter>();

        var snapshots = new[] { "/home", "/orders", "/orders/42" }.Select(route => new RouteableSnapshot(route, new Dictionary<string, string>()));
        var restored = await RouteableState.RestoreSnapshotsAsync(router, snapshots, Ct);

        Assert.True(restored);
        Assert.Equal(2, router.History.Count);                      // the home page, then the sign-in page once, not [home, login, login]
        Assert.IsType<LoginVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_restoration_of_routes_whose_pages_a_guard_redirects_does_not_pile_up_the_redirections()
    {
        await using var provider = Build(services => services.AddSingleton<IRouteGuard, SignInGuard>());
        var router = provider.GetRequiredService<IRouter>();

        var restored = await RouteableState.RestoreAsync(router, ["/home", "/orders", "/orders/42"], Ct);

        Assert.True(restored);
        Assert.Equal(2, router.History.Count);                      // the same answer as RestoreSnapshotsAsync
        Assert.IsType<LoginVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_restoration_of_commands_only_leaves_the_history_as_it_was()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        var none = new Dictionary<string, string>();

        Assert.True(await RouteableState.RestoreSnapshotsAsync(router, [new RouteableSnapshot("-/orders", none), new RouteableSnapshot("-", none)], Ct));
        Assert.True(await RouteableState.RestoreAsync(router, ["-/orders"], Ct));

        Assert.Equal(1, router.History.Count);
        Assert.IsType<HomeVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_restoration_without_redirection_keeps_every_page()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var snapshots = new[] { "/home", "/orders", "/orders/42" }.Select(route => new RouteableSnapshot(route, new Dictionary<string, string>()));

        Assert.True(await RouteableState.RestoreSnapshotsAsync(router, snapshots, Ct));
        Assert.Equal(3, router.History.Count);
        Assert.IsType<OrderVm>(router.CurrentViewModel);
    }
}
