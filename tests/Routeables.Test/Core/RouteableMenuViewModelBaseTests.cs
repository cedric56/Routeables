using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Locking;
using Routeables.Routing;
using System.Collections.ObjectModel;
using Routeables.Abstraction.Registrations;
namespace Routeables.Tests.Core;

public class RouteableMenuViewModelBaseTests
{
    private sealed class PageA;
    private sealed class PageB;

    // The whole of a menu view model: only the entries.
    private sealed class Menu(IRouter router, bool initialize = false) : RouteableMenuViewModelBase(router, initialize)
    {
        public void ViewModelBecameCurrent(object viewModel) => OnViewModelChanged(viewModel);

        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<PageA>("A", route: "/a", template: "/a"),
            new RouteableMenuEntry<PageB>("B", route: "/b", template: "/b"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {

        }
    }

    private static Router CreateRouter()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        return new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), services.BuildServiceProvider());
    }

    [Fact]
    public void Offers_the_entries_without_any_other_code()
    {
        var sut = new Menu(CreateRouter());

        Assert.Equal(["A", "B"], sut.Entries.Select(r => r.DisplayName));
        Assert.Equal("Application", sut.CurrentTitle);
    }

    [Fact]
    public void A_view_model_that_is_in_no_entry_leaves_the_selection_as_it_was()
    {
        var sut = new Menu(CreateRouter());
        sut.ViewModelBecameCurrent(new PageA());

        sut.ViewModelBecameCurrent(new object());

        Assert.Same(sut.Entries[0], sut.SelectedEntry);
    }

    public sealed class StayView : IRouteableFor { public object? Context { get; set; } }
    public sealed class StayVm : IRouteable
    {
        // A page with unsaved changes: it answers at once, so the menu is still inside the setter when the refusal is known.
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;
    }
    public sealed class OtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OtherVm;

    private sealed class GuardedMenu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        public bool InSetter { get; set; }
        public int AnnouncedInSetter { get; private set; }
        public int AnnouncedAfterSetter { get; private set; }

        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<StayVm>("Stay", route: "/stay", template: "/stay"),
            new RouteableMenuEntry<OtherVm>("Other", route: "/other", template: "/other"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {
            if (propertyName != nameof(SelectedEntry)) return;
            if (InSetter) AnnouncedInSetter++; else AnnouncedAfterSetter++;
        }
    }

    [Fact]
    public async Task The_selected_entry_is_announced_again_after_the_setter_when_the_page_refuses_to_be_left()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<StayView, StayVm>(route: "/stay");
        services.RegisterRoute<OtherView, OtherVm>(route: "/other");
        using var provider = services.BuildServiceProvider();
        var sut = new GuardedMenu(provider.GetRequiredService<IRouter>());
        var stay = sut.Entries[0];
        var router = provider.GetRequiredService<IRouter>();
        sut.SelectedEntry = stay;
        while (router.CurrentViewModel is not StayVm)
            await Task.Delay(10, ct);
        await Task.Delay(50, ct);   // the selection that opened the page has ended
        Assert.Same(stay, sut.SelectedEntry);
        var before = sut.AnnouncedAfterSetter;

        sut.InSetter = true;
        sut.SelectedEntry = sut.Entries[1];   // what a menu does when the user clicks an entry
        sut.InSetter = false;

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sut.AnnouncedAfterSetter == before && DateTime.UtcNow < deadline)
            await Task.Delay(10, ct);

        Assert.Same(stay, sut.SelectedEntry);                      // the page is still shown, so is its entry
        Assert.IsType<StayVm>(router.CurrentViewModel);
        Assert.True(sut.AnnouncedAfterSetter > before);        // and the control is told once the setter has returned
    }

    public sealed class ListView : IRouteableFor { public object? Context { get; set; } }
    public sealed class ListVm;
    public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DetailVm;

    private sealed class ListMenu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<ListVm>("List", route: "/list", template: "/list"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {
        }
    }

    [Fact]
    public async Task The_entry_of_the_list_opens_the_list_again_from_a_page_that_has_no_entry()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<ListView, ListVm>(route: "/list");
        services.RegisterRoute<DetailView, DetailVm>(route: "/detail");
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var sut = new ListMenu(router);
        var list = sut.Entries[0];
        Assert.True(await sut.SelectEntryAsync(list));
        Assert.IsType<ListVm>(router.CurrentViewModel);

        // A page opens the detail itself: the menu has no entry for it, so it keeps showing the list as selected.
        Assert.True((await router.GoToAsync("/detail", cancellationToken: ct)).Success);
        Assert.IsType<DetailVm>(router.CurrentViewModel);
        Assert.Same(list, sut.SelectedEntry);

        // The user clicks the entry that is selected: the list is not the page shown, so it opens.
        Assert.True(await sut.SelectEntryAsync(list));
        Assert.IsType<ListVm>(router.CurrentViewModel);
    }

    public sealed class OrdersView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrdersVm;
    public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrderVm(int id) { public int Id { get; } = id; }

    private sealed class OrdersMenu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<OrdersVm>("Orders", route: "/orders", template: "/orders"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {
        }
    }

    [Fact]
    public async Task The_entry_of_the_list_opens_the_list_again_from_a_detail_page_under_its_own_address()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<OrdersView, OrdersVm>(route: "/orders");
        services.RegisterRoute<OrderView, OrderVm>(route: "/orders/{id:int}");
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var sut = new OrdersMenu(router);
        var list = sut.Entries[0];
        Assert.True(await sut.SelectEntryAsync(list));
        Assert.IsType<OrdersVm>(router.CurrentViewModel);

        // The list opens an order: its address starts with the one of the entry, but it is not the list.
        Assert.True((await router.GoToAsync("/orders/42", cancellationToken: ct)).Success);
        Assert.IsType<OrderVm>(router.CurrentViewModel);
        Assert.Same(list, sut.SelectedEntry);

        // The user clicks the entry of the list again: the list opens.
        Assert.True(await sut.SelectEntryAsync(list));
        Assert.IsType<OrdersVm>(router.CurrentViewModel);
    }
}
