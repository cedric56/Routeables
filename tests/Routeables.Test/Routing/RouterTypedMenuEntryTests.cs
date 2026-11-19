using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>A typed menu entry (<c>GoToRouteAsync&lt;T&gt;(entry)</c>) finds the view of its view model whatever it is given: an address that is not its template, only a route, nothing.</summary>
public class RouterTypedMenuEntryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class OrdersView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrdersVm;
    public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrderVm(int id) { public int Id { get; } = id; }
    public sealed class UnroutedView : IRouteableFor { public object? Context { get; set; } }
    public sealed class UnroutedVm { public UnroutedVm() => Built++; public static int Built; }

    private sealed class RefuseAll : IRouteTypeGuard
    {
        public Task<RouteGuardDecision> CanBuildAsync(Type viewModelType, string route, CancellationToken cancellationToken = default)
            => Task.FromResult(RouteGuardDecision.Cancel);
    }

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<OrdersView, OrdersVm>(route: "/orders");
        services.RegisterRoute<OrderView, OrderVm>(route: "/orders/{id:int}");
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_typed_menu_entry_whose_address_is_not_its_template_finds_its_view()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var entry = new RouteableMenuEntry<OrderVm>("Order 5", route: "/orders/5", template: "/orders/{id:int}");

        var result = await router.GoToRouteAsync<OrderVm>(entry, Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(5, Assert.IsType<OrderVm>(router.CurrentViewModel).Id);
    }

    [Fact]
    public async Task A_typed_menu_entry_given_only_a_route_or_nothing_takes_the_route_of_its_view_model()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var withRoute = await router.GoToRouteAsync<OrdersVm>(new RouteableMenuEntry<OrdersVm>("Orders", route: "/orders"), Ct);
        Assert.True(withRoute.Success, withRoute.Exception?.Message);
        await router.GoToAsync("/home", cancellationToken: Ct);

        var withNothing = await router.GoToRouteAsync<OrdersVm>(new RouteableMenuEntry<OrdersVm>("Orders"), Ct);
        Assert.True(withNothing.Success, withNothing.Exception?.Message);
        Assert.IsType<OrdersVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_page_registered_with_no_route_is_asked_of_the_type_guards_before_it_is_built()
    {
        await using var provider = Build(services =>
        {
            services.RegisterRoute<UnroutedView, UnroutedVm>();
            services.AddSingleton<IRouteTypeGuard, RefuseAll>();
        });
        var router = provider.GetRequiredService<IRouter>();
        var before = UnroutedVm.Built;

        var result = await router.GoToRouteAsync<UnroutedVm>(new RouteableMenuEntry<UnroutedVm>("Unrouted"), Ct);

        Assert.False(result.Success);
        Assert.Equal(before, UnroutedVm.Built);
    }
}
