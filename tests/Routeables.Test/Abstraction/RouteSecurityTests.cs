using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public sealed class TenantView : IRouteableFor { public object? Context { get; set; } }
public sealed class TenantVm(int id, string tenant) { public int Id { get; } = id; public string Tenant { get; } = tenant; }
public sealed class PlainOrderView : IRouteableFor { public object? Context { get; set; } }
public sealed class PlainOrderVm(int id) { public int Id { get; } = id; }

/// <summary>An address is user input: it must not decide what a page is built with.</summary>
public class RouteSecurityTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<PlainOrderView, PlainOrderVm>(route: "/orders/{id:int}");
        services.RegisterRoute<TenantView, TenantVm>(route: "/secure/{id:int}");
        return services.BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_tracking_parameter_does_not_stop_a_page_from_opening()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/orders/5?utm_source=mail&fbclid=abc", cancellationToken: Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(5, router.GetCurrentViewModel<PlainOrderVm>()!.Id);
    }

    [Fact]
    public async Task The_query_cannot_fill_a_constructor_parameter_the_template_does_not_declare()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        try { await router.GoToAsync("/secure/5?tenant=attacker", cancellationToken: Ct); }
        catch (InvalidOperationException) { }      // nothing supplies the tenant: the page cannot be built

        Assert.Null(router.GetCurrentViewModel<TenantVm>());
    }

    [Fact]
    public async Task A_value_given_by_the_application_is_not_overridden_by_the_query()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var args = RouteableArgs.Create().WithViewModel("tenant", "trusted");

        var result = await router.GoToAsync("/secure/5?tenant=attacker", args, cancellationToken: Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal("trusted", router.GetCurrentViewModel<TenantVm>()!.Tenant);
    }

    [Fact]
    public async Task Undeclared_query_values_stay_available_to_the_lifecycle_hooks()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/orders/5?utm_source=mail", cancellationToken: Ct);

        Assert.Equal("mail", router.History.Current!.Args.Values.Single(v => v.name == "utm_source").value);
    }
}
