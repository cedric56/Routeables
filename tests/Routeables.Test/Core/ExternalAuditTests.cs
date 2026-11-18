using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Core;

/// <summary>Faults found by reading the code (an external audit of 18 November): each of these failed before it was fixed.</summary>
public class ExternalAuditTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class LoginView : IRouteableFor { public object? Context { get; set; } }
    public sealed class LoginVm;
    public sealed class OrdersView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrdersVm;
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
