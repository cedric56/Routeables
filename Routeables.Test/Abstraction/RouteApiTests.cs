using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }
public sealed class OrderVm(int id) : IRouteable { public int Id { get; } = id; }
public sealed class AboutView : IRouteableFor { public object? Context { get; set; } }
public sealed class AboutVm;
public sealed class RangeView : IRouteableFor { public object? Context { get; set; } }
public sealed class RangeVm(int from, int to, string label) { public int From { get; } = from; public int To { get; } = to; public string Label { get; } = label; }
public sealed class BrokenView : IRouteableFor { public object? Context { get; set; } }
public sealed class BrokenVm : IRouteable
{
    public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default) => throw new InvalidOperationException("boom");
}

/// <summary>Routing by address, arguments given by name, a typed current view model, and errors that reach the sink.</summary>
public class RouteApiTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Error)> Errors { get; } = [];
        public void Report(string source, Exception exception) => Errors.Add((source, exception));
    }

    private static ServiceProvider Build(Sink? sink = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRegion("side");
        services.RegisterRoute<OrderView, OrderVm>(route: "/orders/{id:int}");
        services.RegisterRoute<AboutView, AboutVm>(route: "/about");
        services.RegisterRoute<RangeView, RangeVm>(route: "/range");
        services.RegisterRoute<BrokenView, BrokenVm>(route: "/broken");
        if (sink is not null) services.AddSingleton<IRouteableErrorSink>(sink);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task An_address_is_enough_to_route_the_template_is_found_from_the_route()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/orders/42", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(42, router.GetCurrentViewModel<OrderVm>()!.Id);
    }

    [Fact]
    public async Task The_leading_slash_is_optional_and_a_region_can_be_named()
    {
        using var provider = Build();

        var result = await provider.GoToAsync("orders/7", region: "side", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(7, provider.GetRouter("side").GetCurrentViewModel<OrderVm>()!.Id);
        Assert.Null(provider.GetRequiredService<IRouter>().CurrentViewModel);
    }

    [Fact]
    public async Task An_address_no_route_matches_fails_without_throwing_and_says_what_to_register()
    {
        using var provider = Build();

        var result = await provider.GoToAsync("/nowhere/1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("/nowhere/1", result.Exception!.Message);
        Assert.Contains("RegisterRoute", result.Exception.Message);
    }

    [Fact]
    public async Task The_typed_view_model_is_null_when_it_is_another_type()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/about", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(router.GetCurrentViewModel<AboutVm>());
        Assert.Null(router.GetCurrentViewModel<OrderVm>());
    }

    [Fact]
    public async Task Arguments_are_given_to_the_constructor_parameters_of_their_name_in_any_order()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var args = RouteableArgs.Create()
            .WithViewModel("label", "week")
            .WithViewModel("to", 9)
            .WithViewModel("from", 3);

        var result = await router.GoToAsync("/range", args, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        var vm = router.GetCurrentViewModel<RangeVm>()!;
        Assert.Equal((3, 9, "week"), (vm.From, vm.To, vm.Label));
    }

    [Fact]
    public async Task A_failing_routed_callback_reaches_the_error_sink_and_routing_goes_on()
    {
        var sink = new Sink();
        using var provider = Build(sink);

        var result = await provider.GoToAsync("/broken", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains(sink.Errors, e => e.Source.Contains("BrokenVm") && e.Error.Message == "boom");
    }

    [Fact]
    public async Task The_template_of_a_route_is_optional_it_is_found_from_the_route()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToRouteAsync<OrderVm>("/orders/5", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(5, router.GetCurrentViewModel<OrderVm>()!.Id);

        // A route without parameter is its own template.
        Assert.True((await router.GoToRouteAsync<AboutVm>("/about", cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.NotNull(router.GetCurrentViewModel<AboutVm>());
    }
}
