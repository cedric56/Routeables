using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
namespace Routeables.Tests.Routing;

/// <summary>"/search" and "/search?mode=grid" are two routes: registering both keeps both, whatever the order, and each address shows its own page.</summary>
public class QueryOnlyRoutesTests
{
    public sealed class QoListView : IRouteableFor { public object? Context { get; set; } }
    public sealed class QoListVm;
    public sealed class QoGridView : IRouteableFor { public object? Context { get; set; } }
    public sealed class QoGridVm;

    private static ServiceProvider Build(bool queryFirst)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        if (queryFirst)
        {
            services.RegisterRoute<QoGridView, QoGridVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search?mode=grid");
            services.RegisterRoute<QoListView, QoListVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search");
        }
        else
        {
            services.RegisterRoute<QoListView, QoListVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search");
            services.RegisterRoute<QoGridView, QoGridVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search?mode=grid");
        }
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Each_address_shows_the_page_of_its_own_route_whatever_the_order_of_the_registrations(bool queryFirst)
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(queryFirst);
        var router = provider.GetRequiredService<IRouter>();

        Assert.True((await router.GoToAsync("/qo-search?mode=grid", cancellationToken: ct)).Success);
        Assert.IsType<QoGridVm>(router.CurrentViewModel);

        Assert.True((await router.GoToAsync("/qo-search", cancellationToken: ct)).Success);
        Assert.IsType<QoListVm>(router.CurrentViewModel);
    }
}
