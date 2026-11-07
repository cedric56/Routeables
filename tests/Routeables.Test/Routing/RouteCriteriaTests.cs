using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Routing;

/// <summary>
/// The capabilities of the matrix of the MAUI routing gist that are about typed routes, one registration place and the state as
/// data: each test is the proof of one row (docs/guides/criteria.md lists them).
/// </summary>
public class RouteCriteriaTests
{
    public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrderVm(int id, string? note = null, bool rush = false)
    {
        public int Id { get; } = id;
        public string? Note { get; } = note;
        public bool Rush { get; } = rush;
    }

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm { }

    public sealed class ConfirmView : IRouteableModalFor<ConfirmVm, bool>
    {
        public object? Context { get; set; }
        public Task<bool> ShowModalAsync() => Task.FromResult(true);
    }
    public sealed class ConfirmVm { }

    private static Microsoft.Extensions.DependencyInjection.ServiceProvider Build(Action<RouteBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddRoutes(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_page_is_reached_by_its_view_model_without_writing_a_route()
    {
        using var provider = Build(n => n.Page<OrderView, OrderVm>("/orders/{id:int}?{note?}&{rush:bool?}"));
        var router = provider.GetRequiredService<IRouter>();
        var values = new Dictionary<string, object?> { ["id"] = 42, ["note"] = "a b&c", ["rush"] = true };

        var result = await router.GoToAsync<OrderVm>(values, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var vm = Assert.IsType<OrderVm>(router.CurrentViewModel);
        Assert.Equal(42, vm.Id);
        Assert.Equal("a b&c", vm.Note);
        Assert.True(vm.Rush);
        Assert.Equal("/orders/42?note=a%20b%26c&rush=true", router.History.Current!.Args.Route);
    }

    [Fact]
    public async Task The_values_can_be_the_properties_of_an_object()
    {
        using var provider = Build(n => n.Page<OrderView, OrderVm>("/orders/{id:int}"));
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync<OrderVm>(new { Id = 7 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(7, Assert.IsType<OrderVm>(router.CurrentViewModel).Id);
    }

    [Fact]
    public async Task An_optional_value_that_is_missing_leaves_its_segment_out()
    {
        using var provider = Build(n => n.Page<OrderView, OrderVm>("/orders/{id:int}/{note?}"));
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync<OrderVm>(new Dictionary<string, object?> { ["id"] = 3 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("/orders/3", router.History.Current!.Args.Route);
    }

    [Fact]
    public async Task A_missing_required_value_is_a_failed_result_that_names_it()
    {
        using var provider = Build(n => n.Page<OrderView, OrderVm>("/orders/{id:int}"));
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync<OrderVm>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("{id}", result.Exception!.Message);
    }

    [Fact]
    public async Task A_value_that_breaks_its_constraint_is_refused()
    {
        using var provider = Build(n => n.Page<OrderView, OrderVm>("/orders/{id:int}"));
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync<OrderVm>(new Dictionary<string, object?> { ["id"] = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task A_view_model_without_a_route_says_how_to_give_it_one()
    {
        using var provider = Build(n => { });
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync<OrderVm>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(nameof(OrderVm), result.Exception!.Message);
    }

    [Fact]
    public void Numbers_are_written_with_the_invariant_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("/price/1.5", RouteParser.Build("/price/{amount:double}", new Dictionary<string, object?> { ["amount"] = 1.5 }));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task Pages_tabs_and_modals_are_declared_in_one_place()
    {
        using var provider = Build(n => n
            .Tab<HomeView, HomeVm>("Home", route: "/home", icon: "house")
            .Page<OrderView, OrderVm>("/orders/{id:int}")
            .Modal<ConfirmView, ConfirmVm>());
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;

        // The page and the tab are routes.
        Assert.True((await router.GoToAsync("/home", cancellationToken: ct)).Success);
        Assert.IsType<HomeVm>(router.CurrentViewModel);
        Assert.True((await router.GoToAsync<OrderVm>(new Dictionary<string, object?> { ["id"] = 1 }, cancellationToken: ct)).Success);

        // The modal answers with its result.
        Assert.True(await provider.ShowModalAsync<ConfirmVm, bool>());
    }

    public sealed class MenuMain : IMainViewModel
    {
        public System.Collections.ObjectModel.ObservableCollection<IMenuEntry> Entries { get; } = [];
        public IMenuEntry? SelectedEntry { get; set; }
        public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
        public event EventHandler? LockChanged { add { } remove { } }
    }

    [Fact]
    public void A_tab_is_a_page_and_an_entry_of_the_menu()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<MenuMain>(ServiceLifetime.Singleton);
        services.AddRoutes(n => n.Tab<HomeView, HomeVm>("Home", route: "/home", icon: "house"));
        using var provider = services.BuildServiceProvider();

        var entry = Assert.Single(provider.GetRequiredService<IMainViewModel>().Entries);

        Assert.Equal("Home", entry.DisplayName);
        Assert.Equal("/home", entry.Route);
        Assert.Equal("house", entry.Path);
    }

    public sealed class FormView : IRouteableFor { public object? Context { get; set; } }
    public sealed class FormVm { }

    [Fact]
    public async Task The_values_a_page_was_reached_with_come_back_with_the_page()
    {
        using var provider = Build(n => n.Page<FormView, FormVm>("/form"));
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var when = new DateTime(2026, 11, 7, 10, 30, 0, DateTimeKind.Utc);
        await router.GoToAsync("/form", Routeables.Routing.RouteableArgs.Create()
            .WithValue("owner", "a|b=c\nd").WithValue("count", 3).WithValue("ratio", 1.25).WithValue("id", id).WithValue("when", when).WithValue("on", true).WithValue("skipped", new object()), null, ct);

        var text = RouteableState.Serialize(RouteableState.CaptureSnapshots(router));
        await router.EraseHistoryAsync(ct);
        await RouteableState.RestoreSnapshotsAsync(router, RouteableState.DeserializeSnapshots(text), ct);

        var values = router.History.Current!.Args.Values.ToDictionary(v => v.name!, v => v.value);
        Assert.Equal("a|b=c\nd", values["owner"]);
        Assert.Equal(3, values["count"]);
        Assert.Equal(1.25, values["ratio"]);
        Assert.Equal(id, values["id"]);
        Assert.Equal(when, values["when"]);
        Assert.Equal(true, values["on"]);
        Assert.False(values.ContainsKey("skipped"));
    }

    [Fact]
    public async Task The_values_of_the_address_are_not_kept_twice()
    {
        using var provider = Build(n => n.Page<OrderView, OrderVm>("/orders/{id:int}"));
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;
        await router.GoToAsync("/orders/5?utm=x", cancellationToken: ct);

        var snapshots = RouteableState.CaptureSnapshots(router);
        await RouteableState.RestoreSnapshotsAsync(router, snapshots, ct);

        Assert.Equal(1, router.History.Current!.Args.Values.Count(v => v.name == "utm"));
    }

    [Fact]
    public async Task A_page_shown_with_its_own_view_is_saved_when_it_is_given_a_route()
    {
        using var provider = Build(n => n.Page<FormView, FormVm>("/form"));
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;
        await router.GoAsync(new FormView(), new FormVm(), Routeables.Routing.RouteableArgs.Create().WithRoute("/form"), ct);
        await router.GoAsync(new FormView(), new FormVm(), null, ct);   // no route: not saved

        var routes = RouteableState.Capture(router);

        Assert.Single(routes);
        Assert.Equal("/form", routes[0]);
    }
}
