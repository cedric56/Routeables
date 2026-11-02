using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>Routing by route template through a real <see cref="Router"/> and real route registrations.</summary>
public class RouterGenericRouteTests
{
    public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }

    public sealed class DetailVm(int id, string title = "none")
    {
        public int Id { get; } = id;
        public string Title { get; } = title;
    }

    public sealed record Animal(string Name);

    public sealed class ProbeView : IRouteableFor { public object? Context { get; set; } }

    private static IRouter Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<IRouter>();
    }

    [Fact]
    public async Task GoToAsync_resolves_the_view_model_from_the_route_template()
    {
        // title is declared in the template: a query value the template does not declare never reaches the constructor.
        const string template = "details/{id:int}?{title?}";
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: template));

        var result = await router.GoToAsync(
            "details/42?title=hello", template: template, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var detail = Assert.IsType<DetailVm>(router.CurrentViewModel);
        Assert.Equal(42, detail.Id);
        Assert.Equal("hello", detail.Title);
    }

    [Fact]
    public async Task GoToAsync_reports_a_route_that_does_not_match_its_template()
    {
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: "details/{id:int}"));

        var result = await router.GoToAsync(
            "details/not-a-number", template: "details/{id:int}", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(router.CurrentViewModel);
    }

    [Fact]
    public async Task GoToAsync_rejects_a_null_route()
    {
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: "details/{id:int}"));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            router.GoToAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GoToAsync_finds_the_template_of_a_route_given_without_one()
    {
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: "details/{id:int}"));

        var result = await router.GoToAsync("details/7?title=x", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(7, Assert.IsType<DetailVm>(router.CurrentViewModel).Id);
    }

    public sealed class MenuVm;

    [Fact]
    public async Task GoToRouteAsync_goes_to_a_route_by_its_name_with_the_view_model_given()
    {
        var router = Build(s => s.RegisterRoute<ProbeView, MenuVm>(route: "MenuView"));

        var result = await router.GoToRouteAsync<MenuVm>("MenuView", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.IsType<MenuVm>(router.CurrentViewModel);
        Assert.IsType<ProbeView>(router.CurrentView);
    }

    [Fact]
    public async Task GoToRouteAsync_finds_the_template_of_a_route_given_without_one()
    {
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: "details/{id:int}"));

        var result = await router.GoToRouteAsync<DetailVm>("details/7?title=x", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(7, Assert.IsType<DetailVm>(router.CurrentViewModel).Id);
    }

    [Fact]
    public async Task GoToRouteAsync_takes_the_template_it_is_given()
    {
        const string template = "details/{id:int}?{title?}";
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: template));

        var result = await router.GoToRouteAsync<DetailVm>("details/9?title=hi", template: template, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var detail = Assert.IsType<DetailVm>(router.CurrentViewModel);
        Assert.Equal((9, "hi"), (detail.Id, detail.Title));
    }

    [Fact]
    public async Task GoToRouteAsync_rejects_a_null_route()
    {
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: "details/{id:int}"));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            router.GoToRouteAsync<DetailVm>((string)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_extension_asks_the_router_for_one_request_with_the_route_the_view_model_and_the_view_keys()
    {
        var router = Substitute.For<IRouter>();

        _ = router.GoToRouteAsync<DetailVm>("details/7", viewKeys: ["id"], cancellationToken: TestContext.Current.CancellationToken);

        _ = router.Received(1).GoToRouteAsync(
            Arg.Is<RouteableRouteRequest>(r => r.Route == "details/7" && r.ViewModelType == typeof(DetailVm)
                && r.Entry == null && r.ViewKeys.SequenceEqual(new[] { "id" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_request_with_neither_an_entry_nor_a_route_is_refused()
    {
        var router = Build(s => s.RegisterRoute<DetailView, DetailVm>(route: "details/{id:int}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            router.GoToRouteAsync(new RouteableRouteRequest { ViewModelType = typeof(DetailVm) }, TestContext.Current.CancellationToken));
    }
}
