using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Tests.Pages;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>What a menu view model does when a selection is refused, fails, is cancelled or is redirected, and the other ways its entries can go wrong.</summary>
public class RouteableMenuViewModelBaseEdgeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Menu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        public List<string> NotShown { get; } = [];
        public List<Exception> Failures { get; } = [];
        public bool ThrowWhenNotShown { get; set; }

        public void Became(object? viewModel) => OnViewModelChanged(viewModel);

        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<FakeViewModel>("A", route: "/a", template: "/a"),
            new RouteableMenuEntry<SecondViewModel>("B", route: "/b", template: "/b"),
            new RouteableMenuEntry<ThirdViewModel>("Missing", route: "/missing", template: "/missing"),   // no route is registered for it
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {
        }

        protected override void OnRoutingFailed(IRouteableMenuEntry entry, RouteableResult result)
        {
            NotShown.Add(entry.Route);
            if (ThrowWhenNotShown)
                throw new InvalidOperationException("the hook failed");
            base.OnRoutingFailed(entry, result);
        }

        protected override void OnSelectionFailed(Exception exception) => Failures.Add(exception);
    }

    private sealed class ThrowingMenu(IRouter router, Exception failure) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() => throw failure;

        protected override void NotifyPropertyChanged(string? propertyName)
        {
        }
    }

    private sealed class RedirectGuard : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(change.To.ViewModel is SecondViewModel ? RouteGuardDecision.Redirect("/a") : RouteGuardDecision.Allow);
    }

    // Sends the second page to a page that has no entry in the menu.
    private sealed class RedirectToUnlistedGuard : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(change.To.ViewModel is SecondViewModel ? RouteGuardDecision.Redirect("/d") : RouteGuardDecision.Allow);
    }

    private static ServiceProvider Build(bool redirect = false, bool toUnlisted = false)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/a");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(route: "/b");
        if (redirect)
            services.AddSingleton<IRouteGuard, RedirectGuard>();
        if (toUnlisted)
        {
            services.RegisterRoute<FourthViewFor, FourthViewModel>(route: "/d");
            services.AddSingleton<IRouteGuard, RedirectToUnlistedGuard>();
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Selecting_an_entry_shows_its_page_and_selecting_it_again_changes_nothing()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var sut = new Menu(router);
        var announced = 0;
        sut.CurrentViewChanged += (_, _) => announced++;

        Assert.True(await sut.SelectEntryAsync(sut.Entries[1], cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await sut.SelectEntryAsync(sut.Entries[1], cancellationToken: TestContext.Current.CancellationToken));

        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
        Assert.Same(sut.Entries[1], sut.SelectedEntry);
        Assert.True(announced >= 1);
        Assert.Equal("B", sut.CurrentTitle);
    }

    [Fact]
    public async Task An_entry_whose_route_cannot_be_shown_leaves_the_menu_on_the_entry_it_was_on()
    {
        using var provider = Build();
        var sut = new Menu(provider.GetRequiredService<IRouter>());
        Assert.True(await sut.SelectEntryAsync(sut.Entries[0], cancellationToken: TestContext.Current.CancellationToken));

        var selected = await sut.SelectEntryAsync(sut.Entries[2], cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(selected);
        Assert.Equal(["/missing"], sut.NotShown);
        Assert.Same(sut.Entries[0], sut.SelectedEntry);
        Assert.Empty(sut.Failures);
    }

    [Fact]
    public async Task A_hook_that_throws_when_the_page_is_not_shown_is_reported_and_the_menu_goes_back()
    {
        using var provider = Build();
        var sut = new Menu(provider.GetRequiredService<IRouter>()) { ThrowWhenNotShown = true };
        Assert.True(await sut.SelectEntryAsync(sut.Entries[0], cancellationToken: TestContext.Current.CancellationToken));

        var selected = await sut.SelectEntryAsync(sut.Entries[2], cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(selected);
        Assert.Same(sut.Entries[0], sut.SelectedEntry);
        Assert.Equal("the hook failed", Assert.Single(sut.Failures).Message);
    }

    [Fact]
    public async Task A_selection_whose_token_is_cancelled_answers_no_and_selects_nothing()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var sut = new Menu(router);
        Assert.True(await sut.SelectEntryAsync(sut.Entries[0], cancellationToken: TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var selected = await sut.SelectEntryAsync(sut.Entries[1], cancelled.Token);

        Assert.False(selected);
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);
        Assert.Empty(sut.Failures);                    // a cancelled selection is not a failure
    }

    [Fact]
    public async Task A_guard_that_redirects_leaves_the_menu_on_the_entry_of_the_page_that_is_shown()
    {
        using var provider = Build(redirect: true);
        var router = provider.GetRequiredService<IRouter>();
        var sut = new Menu(router);
        Assert.True(await sut.SelectEntryAsync(sut.Entries[0], cancellationToken: TestContext.Current.CancellationToken));

        await sut.SelectEntryAsync(sut.Entries[1], cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<FakeViewModel>(router.CurrentViewModel);
        Assert.NotSame(sut.Entries[1], sut.SelectedEntry);
    }

    [Fact]
    public async Task A_guard_that_redirects_to_a_page_with_no_entry_puts_the_menu_back_on_the_entry_it_was_on()
    {
        using var provider = Build(toUnlisted: true);
        var router = provider.GetRequiredService<IRouter>();
        var sut = new Menu(router);
        Assert.True(await sut.SelectEntryAsync(sut.Entries[0], Ct));

        await sut.SelectEntryAsync(sut.Entries[1], Ct);

        Assert.IsType<FourthViewModel>(router.CurrentViewModel);
        Assert.Same(sut.Entries[0], sut.SelectedEntry);
    }

    [Fact]
    public async Task Selecting_no_entry_without_a_token_answers_yes_and_leaves_the_page_alone()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var sut = new Menu(router);
        Assert.True(await sut.SelectEntryAsync(sut.Entries[0], Ct));

        var selected = await sut.SelectEntryAsync(null, TestContext.Current.CancellationToken);

        Assert.True(selected);
        Assert.Null(sut.SelectedEntry);
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Going_back_and_forward_through_the_menu_moves_the_router()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var sut = new Menu(router);
        await router.GoToAsync("/a", cancellationToken: Ct);
        await router.GoToAsync("/b", cancellationToken: Ct);

        await sut.GoBackAsync();
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);

        await sut.GoForwardAsync();
        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
    }

    [Fact]
    public async Task A_page_that_becomes_current_with_no_view_model_changes_nothing()
    {
        using var provider = Build();
        var sut = new Menu(provider.GetRequiredService<IRouter>());
        Assert.True(await sut.SelectEntryAsync(sut.Entries[1], cancellationToken: TestContext.Current.CancellationToken));

        sut.Became(null);

        Assert.Same(sut.Entries[1], sut.SelectedEntry);
    }

    [Fact]
    public void Entries_that_read_what_does_not_exist_yet_explain_how_to_build_them()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var fromNull = Assert.Throws<InvalidOperationException>(() => new ThrowingMenu(router, new NullReferenceException()).Entries);
        var fromArgument = Assert.Throws<InvalidOperationException>(() => new ThrowingMenu(router, new ArgumentNullException("service")).Entries);

        Assert.Contains("initialize: false", fromNull.Message);
        Assert.IsType<NullReferenceException>(fromNull.InnerException);
        Assert.IsType<ArgumentNullException>(fromArgument.InnerException);
    }

    [Fact]
    public void Another_failure_of_the_entries_is_not_hidden()
    {
        using var provider = Build();

        var failure = Assert.Throws<FormatException>(() => new ThrowingMenu(provider.GetRequiredService<IRouter>(), new FormatException("bad entry")).Entries);

        Assert.Equal("bad entry", failure.Message);
    }

    [Fact]
    public void Notifying_a_lock_change_reaches_the_subscribers_and_none_is_not_an_error()
    {
        using var provider = Build();
        var sut = new Menu(provider.GetRequiredService<IRouter>());
        sut.NotifyLockChanged();
        var raised = 0;
        sut.LockChanged += (_, _) => raised++;

        sut.NotifyLockChanged();

        Assert.Equal(1, raised);
    }
}
