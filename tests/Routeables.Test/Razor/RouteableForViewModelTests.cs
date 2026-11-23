using Microsoft.AspNetCore.Components;
using Routeables.Abstraction.Bases;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Tests.Core;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Razor;

/// <summary><see cref="RouteableFor{TView, TViewModel}"/>: the lifecycle it has by default, whether a menu is locked, and the view model it follows while it is shown.</summary>
public class RouteableForViewModelTests
{
    private sealed class Child : ComponentBase;

    private sealed class Probe<TViewModel> : RouteableFor<Child, TViewModel> where TViewModel : class
    {
        public int Subscribed { get; private set; }
        public int Unsubscribed { get; private set; }

        protected override void OnSubscribe() => Subscribed++;

        protected override void OnUnsubscribe() => Unsubscribed++;

        /// <summary>What the renderer does when the component gets its parameters: the context is the view model of the route.</summary>
        public void Show(TViewModel? viewModel)
        {
#pragma warning disable BL0005
            Context = viewModel;
#pragma warning restore BL0005
            OnParametersSet();
        }
    }

    private static IRouteableMenuEntry Entry(bool locked) => new RouteableMenuEntry<FakeViewModel>("Entry", null, "/entry", "/entry") { IsLock = locked };

    [Fact]
    public async Task By_default_a_view_can_be_left_and_its_hooks_complete()
    {
        var view = new Probe<FakeViewModel>();
        var args = RouteableArgs.Create();
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await view.CanLeaveAsync(ct));
        await view.InitializeAsync(args, ct);
        await view.OnEnteringAsync(args, ct);
        await view.OnLeavingAsync(args, ct);
    }

    [Fact]
    public void A_view_that_is_not_a_menu_is_not_locked()
    {
        var view = new Probe<FakeViewModel>();
        view.Show(new FakeViewModel());

        Assert.False(view.IsLocked);
    }

    [Fact]
    public void A_menu_is_locked_when_it_has_entries_and_all_of_them_are_locked()
    {
        var menu = new FakeMenuViewModel();
        var view = new Probe<FakeMenuViewModel>();
        view.Show(menu);
        Assert.False(view.IsLocked);                 // no entry: nothing to lock

        menu.Entries.Add(Entry(locked: true));
        menu.Entries.Add(Entry(locked: true));
        Assert.True(view.IsLocked);

        menu.Entries.Add(Entry(locked: false));
        Assert.False(view.IsLocked);                 // one that is not locked is enough to keep the menu open
    }

    [Fact]
    public void The_view_follows_the_lock_of_its_menu_once_and_lets_it_go_when_it_is_disposed()
    {
        var menu = new FakeMenuViewModel();
        var view = new Probe<FakeMenuViewModel>();

        view.Show(menu);
        view.Show(menu);                             // the same view model again: nothing to do

        Assert.Equal(1, menu.LockSubscribers);
        Assert.Equal(1, view.Subscribed);

        view.Dispose();

        Assert.Equal(0, menu.LockSubscribers);
        Assert.Equal(1, view.Unsubscribed);
    }

    [Fact]
    public void Another_view_model_replaces_the_one_that_was_followed()
    {
        var first = new FakeMenuViewModel();
        var second = new FakeMenuViewModel();
        var view = new Probe<FakeMenuViewModel>();
        view.Show(first);

        view.Show(second);

        Assert.Equal(0, first.LockSubscribers);
        Assert.Equal(1, second.LockSubscribers);
        Assert.Equal(2, view.Subscribed);
        Assert.Equal(1, view.Unsubscribed);

        view.Show(null);                             // no view model: nothing is followed

        Assert.Equal(0, second.LockSubscribers);
        Assert.Equal(2, view.Unsubscribed);
    }

    [Fact]
    public async Task A_lock_that_changes_while_the_component_is_not_on_a_page_is_not_an_error()
    {
        var menu = new FakeMenuViewModel();
        var view = new Probe<FakeMenuViewModel>();
        view.Show(menu);

        // The handler is async void: an exception would reach the process. The component has no renderer here, so its refresh cannot run.
        menu.NotifyLockChanged();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        view.Dispose();
    }

    [Fact]
    public void A_view_model_that_is_not_a_menu_is_still_subscribed_and_let_go()
    {
        var view = new Probe<FakeViewModel>();
        view.Show(new FakeViewModel());

        view.Dispose();

        Assert.Equal(1, view.Subscribed);
        Assert.Equal(1, view.Unsubscribed);
    }
}
