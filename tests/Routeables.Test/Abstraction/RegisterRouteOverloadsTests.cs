using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// The registrations of a route with a view factory: the views that take the arguments of the route (one to five), the factory that fails, and
/// the route that can be reached from an address.
/// </summary>
public class RegisterRouteOverloadsTests
{
    // A view that keeps what the route gave it.
    private sealed class Arguments(object?[] values)
    {
        public object?[] Values { get; } = values;
    }

    private static IRouteableArgs With(params object[] values)
    {
        var args = RouteableArgs.Create();
        for (var i = 0; i < values.Length; i++)
            args = args.WithView("arg" + i, values[i]);
        return args;
    }

    private static object View(ServiceProvider provider, string route, IRouteableArgs args)
    {
        var key = RouteableKeys.GetKeys(typeof(FakeViewModel), route).viewKey;
        return provider.GetRequiredKeyedService<Func<IServiceProvider, IRouteableArgs, object>>(key)(provider, args);
    }

    [Fact]
    public void A_view_takes_the_first_argument_of_the_route()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<Arguments, FakeViewModel, int>(route: "/one", func: (_, a) => new Arguments([a]));
        using var provider = services.BuildServiceProvider();

        var view = Assert.IsType<Arguments>(View(provider, "/one", With(7)));

        Assert.Equal([7], view.Values);
    }

    [Fact]
    public void A_view_takes_two_arguments_in_the_order_they_were_given()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<Arguments, FakeViewModel, int, string>((_, a, b) => new Arguments([a, b]), route: "/two");
        using var provider = services.BuildServiceProvider();

        var view = Assert.IsType<Arguments>(View(provider, "/two", With(7, "seven")));

        Assert.Equal([7, "seven"], view.Values);
    }

    [Fact]
    public void A_view_takes_three_arguments()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<Arguments, FakeViewModel, int, string, bool>((_, a, b, c) => new Arguments([a, b, c]), route: "/three");
        using var provider = services.BuildServiceProvider();

        var view = Assert.IsType<Arguments>(View(provider, "/three", With(7, "seven", true)));

        Assert.Equal([7, "seven", true], view.Values);
    }

    [Fact]
    public void A_view_takes_four_arguments()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<Arguments, FakeViewModel, int, string, bool, double>((_, a, b, c, d) => new Arguments([a, b, c, d]), route: "/four");
        using var provider = services.BuildServiceProvider();

        var view = Assert.IsType<Arguments>(View(provider, "/four", With(7, "seven", true, 7.5)));

        Assert.Equal([7, "seven", true, 7.5], view.Values);
    }

    [Fact]
    public void A_view_takes_five_arguments()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<Arguments, FakeViewModel, int, string, bool, double, char>((_, a, b, c, d, e) => new Arguments([a, b, c, d, e]), route: "/five");
        using var provider = services.BuildServiceProvider();

        var view = Assert.IsType<Arguments>(View(provider, "/five", With(7, "seven", true, 7.5, 'x')));

        Assert.Equal([7, "seven", true, 7.5, 'x'], view.Values);
    }

    [Fact]
    public void An_argument_of_another_type_than_the_view_asks_for_is_refused_saying_what_it_is()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<Arguments, FakeViewModel, int>(route: "/typed", func: (_, a) => new Arguments([a]));
        using var provider = services.BuildServiceProvider();

        var failure = Assert.Throws<InvalidCastException>(() => View(provider, "/typed", With("not a number")));

        Assert.Contains("System.Int32", failure.Message);
        Assert.Contains("System.String", failure.Message);
    }

    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void A_factory_that_fails_gives_the_exception_to_the_arguments_and_rethrows_it(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute("/boom", (_, _) => throw new InvalidOperationException("the view cannot be built"), lifetime);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var args = RouteableArgs.Create();

        var failure = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredKeyedService<Func<IServiceProvider, IRouteableArgs, object>>("/boom")(scope.ServiceProvider, args));

        Assert.Equal("the view cannot be built", failure.Message);
        Assert.Same(failure, args.Exception);
    }

    [Fact]
    public void A_singleton_that_failed_is_built_again_by_the_next_call()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute("/flaky", (_, _) => ++calls == 1 ? throw new InvalidOperationException("first call fails") : new object(), ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredKeyedService<Func<IServiceProvider, IRouteableArgs, object>>("/flaky");

        Assert.Throws<InvalidOperationException>(() => factory(provider, RouteableArgs.Create()));

        var second = factory(provider, RouteableArgs.Create());
        Assert.Same(second, factory(provider, RouteableArgs.Create()));   // kept from then on
        Assert.Equal(2, calls);
    }

    [Fact]
    public void A_view_factory_and_a_view_model_factory_make_a_page_that_an_address_can_reach()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(
            (_, _) => new FakeViewFor(), (_, _) => new FakeViewModel(), ServiceLifetime.Transient, ServiceLifetime.Transient, "/page");
        using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetRequiredService<RouteableTable>().Entries, e => e.Template == "/page" && e.ViewModelType == typeof(FakeViewModel));
        Assert.IsType<FakeViewFor>(View(provider, "/page", RouteableArgs.Create()));
    }

    [Fact]
    public void A_factory_that_is_missing_is_refused_by_name()
    {
        var services = new ServiceCollection();

        var view = Assert.Throws<ArgumentNullException>(() => services.RegisterRoute<FakeViewFor, FakeViewModel>(
            null!, (_, _) => new FakeViewModel(), ServiceLifetime.Transient, ServiceLifetime.Transient));
        var viewModel = Assert.Throws<ArgumentNullException>(() => services.RegisterRoute<FakeViewFor, FakeViewModel>(
            (_, _) => new FakeViewFor(), null!, ServiceLifetime.Transient, ServiceLifetime.Transient));

        Assert.Equal("viewFactory", view.ParamName);
        Assert.Equal("viewModelFactory", viewModel.ParamName);
    }
}
