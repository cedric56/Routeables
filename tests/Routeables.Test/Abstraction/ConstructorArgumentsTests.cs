using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public class ConstructorArgumentsTests
{
    private sealed class Greeter;

    private static IServiceProvider Provider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static IRouteableArgs Args() => RouteableArgs.Create();

    [Fact]
    public void A_value_given_wins_over_the_container()
    {
        var args = Args().WithViewModel("age", 5);
        var provider = Provider(s => s.AddSingleton(typeof(int), 99));

        var arguments = ConstructorArguments.ForViewModel(provider, args, "Cat", ["age"], takesRouter: false);

        Assert.Equal(5, arguments.Next<int>("age"));
    }

    [Fact]
    public void A_parameter_without_a_value_takes_the_service_of_its_type()
    {
        var greeter = new Greeter();
        var arguments = ConstructorArguments.ForViewModel(Provider(s => s.AddSingleton(greeter)), Args(), "Cat", ["greeter"], takesRouter: false);

        Assert.Same(greeter, arguments.Next<Greeter>("greeter"));
    }

    [Fact]
    public void A_parameter_nothing_can_give_fails_naming_the_type_and_the_parameter()
    {
        var arguments = ConstructorArguments.ForViewModel(Provider(), Args(), "Cat", ["greeter"], takesRouter: false);

        var ex = Assert.Throws<InvalidOperationException>(() => arguments.Next<Greeter>("greeter"));

        Assert.Contains("Cat", ex.Message);
        Assert.Contains("greeter", ex.Message);
        Assert.Contains(nameof(Greeter), ex.Message);
    }

    [Fact]
    public void A_parameter_with_a_default_keeps_it_when_nothing_is_given()
    {
        var arguments = ConstructorArguments.ForViewModel(Provider(), Args(), "Cat", ["age"], takesRouter: false);

        Assert.Equal(3, arguments.Next("age", 3));
    }

    [Fact]
    public void A_value_given_is_used_once()
    {
        var args = Args().WithViewModel("first", "a");
        var arguments = ConstructorArguments.ForViewModel(Provider(), args, "Cat", ["first", "second"], takesRouter: false);

        Assert.Equal("a", arguments.Next<string>("first"));
        Assert.Equal("fallback", arguments.Next("second", "fallback"));
    }

    [Fact]
    public void The_values_follow_the_order_of_the_parameters_not_the_order_given()
    {
        var args = Args().WithViewModel("second", "B").WithViewModel("first", "A");
        var arguments = ConstructorArguments.ForViewModel(Provider(), args, "Cat", ["first", "second"], takesRouter: false);

        Assert.Equal("A", arguments.Next<string>("first"));
        Assert.Equal("B", arguments.Next<string>("second"));
    }

    [Fact]
    public void Attribute_values_are_not_constructor_arguments()
    {
        var args = Args().WithViewModel("nick", "Tom", attribute: true);
        var arguments = ConstructorArguments.ForViewModel(Provider(), args, "Cat", ["nick"], takesRouter: false);

        Assert.Equal("none", arguments.Next("nick", "none"));
    }

    [Fact]
    public void The_router_that_routes_is_given_to_a_view_model_that_takes_one()
    {
        var router = Provider(s => s.RegisterRouting()).GetRequiredService<IRouter>();
        var args = Args();
        ((RouteableArgs)args).Router = router;

        var arguments = ConstructorArguments.ForViewModel(Provider(), args, "Cat", ["router"], takesRouter: true);

        Assert.Same(router, arguments.Next<IRouter>("router"));
    }

    [Fact]
    public void The_router_is_not_added_when_the_view_model_does_not_take_one()
    {
        var router = Provider(s => s.RegisterRouting()).GetRequiredService<IRouter>();
        var args = Args();
        ((RouteableArgs)args).Router = router;

        var arguments = ConstructorArguments.ForViewModel(Provider(), args, "Cat", [], takesRouter: false);

        Assert.Throws<InvalidOperationException>(() => arguments.Next<IRouter>("router"));
    }

    [Fact]
    public void A_view_takes_the_values_given_to_it_in_order_and_ignores_attribute_values()
    {
        var args = Args().WithView("a", "A").WithView("n", 1).WithView("hidden", "H", attribute: true);
        var arguments = ConstructorArguments.ForView(Provider(), args, "CatView");

        Assert.Equal("A", arguments.Next<string>("a"));
        Assert.Equal(1, arguments.Next<int>("n"));
        Assert.Equal("none", arguments.Next("hidden", "none"));
    }
}
