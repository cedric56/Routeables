using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public sealed class ValueView : IRouteableFor { public object? Context { get; set; } }
public sealed class OneIntVm(int id) { public int Id { get; } = id; }
public sealed class TwoConstructorsVm
{
    public TwoConstructorsVm(int id) => Id = id;
    public TwoConstructorsVm(string name) => Name = name;
    public int Id { get; }
    public string? Name { get; }
}

/// <summary>The values a page is built with: those no constructor takes are left out instead of failing the page.</summary>
public class ConstructorValuesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_value_that_no_constructor_parameter_takes_is_left_out_of_a_page_with_one_constructor()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<ValueView, OneIntVm>(route: "/one");
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        // Two numbers for one parameter: the first fills it, the second has nowhere to go and does not make the page fail.
        var args = RouteableArgs.Create().WithViewModel("id", 3).WithViewModel("another", 4);
        var result = await router.GoToAsync("/one", args, cancellationToken: Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(3, router.GetCurrentViewModel<OneIntVm>()!.Id);
    }

    [Fact]
    public async Task A_value_that_no_constructor_takes_is_left_out_of_a_page_with_several_constructors()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<ValueView, TwoConstructorsVm>(route: "/two");
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        var args = RouteableArgs.Create().WithViewModel("name", "x").WithViewModel("flag", true);
        var result = await router.GoToAsync("/two", args, cancellationToken: Ct);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal("x", router.GetCurrentViewModel<TwoConstructorsVm>()!.Name);
    }

    [Fact]
    public void A_lifetime_that_does_not_exist_is_refused_when_a_route_is_registered()
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() =>
            services.RegisterRoute<ValueView, OneIntVm>((ServiceLifetime)99, ServiceLifetime.Transient, "/one"));
    }
}
