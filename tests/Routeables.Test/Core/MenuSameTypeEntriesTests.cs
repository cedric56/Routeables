using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using System.Collections.ObjectModel;
namespace Routeables.Tests.Core;

/// <summary>Several entries of a menu can show the same view model type with other routes: the menu follows the route, not the type.</summary>
public class MenuSameTypeEntriesTests
{
    public sealed class MsProductView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MsProductVm;
    public sealed class MsOtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MsOtherVm;

    private sealed class Menu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<MsProductVm>("Shoes", route: "/ms/shoes", template: "/ms/shoes"),
            new RouteableMenuEntry<MsProductVm>("Hats", route: "/ms/hats", template: "/ms/hats"),
            new RouteableMenuEntry<MsOtherVm>("Other", route: "/ms-other", template: "/ms-other"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName) { }
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<MsProductView, MsProductVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms/shoes");
        services.RegisterRoute<MsProductView, MsProductVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms/hats");
        services.RegisterRoute<MsOtherView, MsOtherVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-other");
        services.AddTransient<MsOtherVm>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Back_selects_the_entry_of_the_page_that_is_shown_and_not_the_first_entry_of_its_type()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);

        Assert.True(await menu.SelectEntryAsync(menu.Entries[1], ct));      // Hats
        Assert.True(await menu.SelectEntryAsync(menu.Entries[2], ct));      // Other
        await router.GoBackAsync(ct);                                       // the router goes back, not the menu: the menu follows it

        Assert.Equal("Hats", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task A_refusal_after_a_back_returns_to_the_entry_that_is_shown()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);
        await menu.SelectEntryAsync(menu.Entries[1], ct);
        await menu.SelectEntryAsync(menu.Entries[2], ct);
        await router.GoBackAsync(ct);

        // The page of "Hats" is shown: selecting it again is the same page, and the menu stays on it.
        await menu.SelectEntryAsync(menu.Entries[1], ct);

        Assert.Equal("Hats", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task Two_overlapping_back_moves_of_the_menu_are_both_followed_by_the_menu_itself()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);
        await menu.SelectEntryAsync(menu.Entries[0], ct);
        await menu.SelectEntryAsync(menu.Entries[1], ct);
        await menu.SelectEntryAsync(menu.Entries[2], ct);

        var first = menu.GoBackAsync();
        var second = menu.GoBackAsync();
        await Task.WhenAll(first, second);

        Assert.Equal("Shoes", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task Going_back_zero_pages_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.MoveBackAsync(0, ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.MoveBackAsync(-1, ct));
    }

    public sealed class MsSecretView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MsSecretVm;
    public sealed class MsLoginView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MsLoginVm;

    private sealed class SecretGuard : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(change.To.ViewModel is MsSecretVm ? RouteGuardDecision.Redirect("/ms-login") : RouteGuardDecision.Allow);
    }

    private sealed class GuardedMenu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<MsOtherVm>("Home", route: "/ms-other", template: "/ms-other"),
            new RouteableMenuEntry<MsSecretVm>("Secret", route: "/ms-secret", template: "/ms-secret"),
            new RouteableMenuEntry<MsLoginVm>("Sign in", route: "/ms-login", template: "/ms-login"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName) { }
    }

    [Fact]
    public async Task A_guard_that_redirects_a_selection_makes_the_menu_follow_the_page_it_redirected_to()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<MsOtherView, MsOtherVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-other");
        services.RegisterRoute<MsSecretView, MsSecretVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-secret");
        services.RegisterRoute<MsLoginView, MsLoginVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-login");
        services.AddTransient<MsOtherVm>();
        services.AddTransient<MsSecretVm>();
        services.AddTransient<MsLoginVm>();
        services.AddSingleton<IRouteGuard>(new SecretGuard());
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new GuardedMenu(router);
        await menu.SelectEntryAsync(menu.Entries[0], ct);

        await menu.SelectEntryAsync(menu.Entries[1], ct);      // "Secret": the guard sends the user to the sign-in page

        Assert.IsType<MsLoginVm>(router.CurrentViewModel);
        Assert.Equal("Sign in", menu.SelectedEntry?.DisplayName);
    }

    private sealed class BlockingGuard : IRouteGuard
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
        {
            if (change.To.ViewModel is not MsSecretVm)
                return RouteGuardDecision.Allow;
            Asked.TrySetResult();
            await Answer.Task.WaitAsync(cancellationToken);
            return RouteGuardDecision.Allow;
        }
    }

    [Fact]
    public async Task A_selection_cancelled_while_it_waits_for_the_one_that_runs_answers_no_and_leaves_the_menu_on_the_entry_that_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var guard = new BlockingGuard();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<MsOtherView, MsOtherVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-other");
        services.RegisterRoute<MsSecretView, MsSecretVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-secret");
        services.RegisterRoute<MsLoginView, MsLoginVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-login");
        services.AddTransient<MsOtherVm>();
        services.AddTransient<MsSecretVm>();
        services.AddTransient<MsLoginVm>();
        services.AddSingleton<IRouteGuard>(guard);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new GuardedMenu(router);
        await menu.SelectEntryAsync(menu.Entries[0], ct);
        var running = menu.SelectEntryAsync(menu.Entries[1], ct);        // "Secret": the guard holds the gate
        await guard.Asked.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var waiting = menu.SelectEntryAsync(menu.Entries[2], source.Token);   // "Sign in": it waits for the gate
        await Task.Delay(50, ct);

        await source.CancelAsync();

        Assert.False(await waiting);
        Assert.Equal("Secret", menu.SelectedEntry?.DisplayName);
        guard.Answer.SetResult();
        Assert.True(await running);
        Assert.IsType<MsSecretVm>(router.CurrentViewModel);
        Assert.Equal("Secret", menu.SelectedEntry?.DisplayName);
    }

    private sealed class MenuOfAField : RouteableMenuViewModelBase
    {
        private readonly string[] _names;

        public MenuOfAField(IRouter router, string[] names, bool initialize) : base(router, initialize) => _names = names;

        protected override ObservableCollection<IRouteableMenuEntry> GetEntries()
            => new(_names.Select(name => (IRouteableMenuEntry)new RouteableMenuEntry<MsOtherVm>(name, route: "/ms-other", template: "/ms-other")));

        protected override void NotifyPropertyChanged(string? propertyName) { }
    }

    [Fact]
    public void Entries_built_from_a_field_while_the_base_is_constructed_say_why_they_failed()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var failure = Assert.Throws<InvalidOperationException>(() => new MenuOfAField(router, ["Other"], initialize: true));

        Assert.Contains("initialize: false", failure.Message);
        Assert.IsType<ArgumentNullException>(failure.InnerException);      // the null field handed to Select
    }

    [Fact]
    public void With_initialize_false_the_entries_can_be_built_from_a_field()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var menu = new MenuOfAField(router, ["Other"], initialize: false);

        Assert.Equal("Other", Assert.Single(menu.Entries).DisplayName);
    }
}
