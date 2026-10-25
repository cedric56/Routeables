using System.Collections.ObjectModel;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Services;

/// <summary>The answer of every host to the Back request of the system: one page back when there is one, otherwise the system decides.</summary>
public class RouteableBackTests
{
    public sealed class BackFirstView : IRouteableFor { public object? Context { get; set; } }
    public sealed class BackFirstVm;
    public sealed class BackSecondView : IRouteableFor { public object? Context { get; set; } }
    public sealed class BackSecondVm;

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<BackFirstView, BackFirstVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/back-first");
        services.RegisterRoute<BackSecondView, BackSecondVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/back-second");
        services.AddTransient<BackFirstVm>();
        services.AddTransient<BackSecondVm>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static async Task WaitAsync(Func<bool> condition, CancellationToken ct)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10, ct);

        Assert.True(condition());
    }

    [Fact]
    public void A_container_without_a_router_leaves_the_request_to_the_system()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.False(RouteableBack.TryGoBack(provider));
    }

    [Fact]
    public async Task On_the_first_page_the_request_is_left_to_the_system()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/back-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(RouteableBack.TryGoBack(provider));
        Assert.IsType<BackFirstVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task With_a_page_to_go_back_to_the_request_is_handled_and_the_router_goes_back()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/back-first", cancellationToken: ct);
        await router.GoToAsync("/back-second", cancellationToken: ct);

        Assert.True(RouteableBack.TryGoBack(provider));

        await WaitAsync(() => router.CurrentViewModel is BackFirstVm, ct);
        Assert.False(router.CanGoBack);
        Assert.True(router.CanGoForward);
        // At the first page again, the next request is the system's.
        Assert.False(RouteableBack.TryGoBack(provider));
    }

    [Fact]
    public async Task A_page_that_refuses_to_be_left_keeps_the_user_where_they_are_and_the_request_is_still_handled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build(services => services.AddSingleton<IRouteGuard>(new RefuseBack()));
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/back-first", cancellationToken: ct);
        await router.GoToAsync("/back-second", cancellationToken: ct);
        var routed = new TaskCompletionSource<RouteableOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        router.Routed += (_, e) => routed.TrySetResult(e.Outcome);

        Assert.True(RouteableBack.TryGoBack(provider));

        Assert.Equal(RouteableOutcome.Cancelled, await routed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct));
        Assert.IsType<BackSecondVm>(router.CurrentViewModel);
    }

    private sealed class RefuseBack : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(change.Kind == RouteableKind.Back ? RouteGuardDecision.Cancel : RouteGuardDecision.Allow);
    }

    [Fact]
    public void A_provider_is_required() => Assert.Throws<ArgumentNullException>(() => RouteableBack.TryGoBack(null!));

    private sealed class TabMenu : IMainRouteables
    {
        public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
        public IRouteableEntry? Routeable { get; set; }
        public object? CurrentView { get; set; }
        public string? CurrentTitle => null;
        public EventHandler<object?>? CurrentViewChanged { get; set; }
        public EventHandler? LockChanged { get; set; }
        public object? Moved { get; private set; }
        public void OnRouterMoved(object? viewModel) => Moved = viewModel;
    }

    [Fact]
    public async Task A_tab_with_its_own_history_goes_back_inside_the_tab_first()
    {
        var menu = new TabMenu();
        using var provider = Build(services =>
        {
            services.AddRegion("back-tab");
            services.AddSingleton<IMainRouteables>(menu);
        });
        var ct = TestContext.Current.CancellationToken;
        var main = provider.GetRequiredService<IRouter>();
        var tab = provider.GetRouter("back-tab");
        await main.GoToAsync("/back-first", cancellationToken: ct);
        await main.GoToAsync("/back-second", cancellationToken: ct);
        await tab.GoToAsync("/back-first", cancellationToken: ct);
        await tab.GoToAsync("/back-second", cancellationToken: ct);
        menu.Routeable = new RouteableEntry<BackFirstVm>("Tab", route: "/back-first", template: "/back-first") { Region = "back-tab" };

        Assert.Same(tab, RouteableBack.Target(provider));
        Assert.True(RouteableBack.TryGoBack(provider));
        await WaitAsync(() => !tab.CanGoBack, ct);

        Assert.True(main.CanGoBack);
        Assert.IsType<BackSecondVm>(main.CurrentViewModel);
        Assert.Same(main, RouteableBack.Target(provider));
    }

    [Fact]
    public async Task The_menu_is_told_which_page_is_shown_after_the_router_went_back()
    {
        var menu = new TabMenu();
        using var provider = Build(services => services.AddSingleton<IMainRouteables>(menu));
        var ct = TestContext.Current.CancellationToken;
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/back-first", cancellationToken: ct);
        await router.GoToAsync("/back-second", cancellationToken: ct);

        Assert.True(RouteableBack.TryGoBack(provider));

        await WaitAsync(() => menu.Moved is BackFirstVm, ct);
        Assert.Same(router.CurrentViewModel, menu.Moved);
    }
}
