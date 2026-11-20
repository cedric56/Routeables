using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Routing;

/// <summary>"/search" and "/search?mode=grid" are two routes: registering both keeps both, whatever the order, and each address shows its own page.</summary>
public class QueryOnlyRoutesTests
{

    private static ServiceProvider Build(bool queryFirst)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        if (queryFirst)
        {
            services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search?mode=grid");
            services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search");
        }
        else
        {
            services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search");
            services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/qo-search?mode=grid");
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
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);

        Assert.True((await router.GoToAsync("/qo-search", cancellationToken: ct)).Success);
        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
    }

    [Theory]
    [InlineData("/qo-bad/{id?:int}", "/qo-bad/5")]
    [InlineData("/qo-bad?q={q}", "/qo-bad?q=shoes")]
    public async Task A_template_with_a_misplaced_placeholder_is_named_when_no_route_matches(string template, string address)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, template);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync(address, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("read as fixed text", result.Exception?.Message);
    }
}
