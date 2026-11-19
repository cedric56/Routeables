using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>Which constructor parameter a value of the address goes to, and which template a route is read with, when the router builds a page by reflection.</summary>
public class RouterValueBindingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class OrdersView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrdersVm;

    public sealed class ProductView : IRouteableFor { public object? Context { get; set; } }
    public sealed class ProductVm(string category = "all", string id = "")
    {
        public string Category { get; } = category;
        public string Id { get; } = id;
    }

    public sealed class TabView : IRouteableFor { public object? Context { get; set; } }
    public sealed class TabVm;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<OrdersView, OrdersVm>(route: "/orders");
        services.RegisterRoute<ProductView, ProductVm>(route: "/products/{id}");
        services.RegisterRoute<TabView, TabVm>(route: "/tabs/{tab:int}");
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_value_named_for_a_later_parameter_does_not_go_to_an_earlier_one_of_the_same_type_by_reflection()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        Assert.True((await router.GoToAsync("/products/42", cancellationToken: Ct)).Success);

        var page = Assert.IsType<ProductVm>(router.CurrentViewModel);
        Assert.Equal("all", page.Category);
        Assert.Equal("42", page.Id);
    }

    [Fact]
    public async Task A_route_value_the_constructor_does_not_ask_for_is_left_out_by_reflection_as_it_is_by_the_generated_code()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/tabs/3", cancellationToken: Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.IsType<TabVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_template_given_without_its_first_slash_is_the_registered_template()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/orders", template: "orders", cancellationToken: Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.IsType<OrdersVm>(router.CurrentViewModel);
    }
}
