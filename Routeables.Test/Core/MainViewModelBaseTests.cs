using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using System.Collections.ObjectModel;
using Routeables.Locking;

namespace Routeables.Tests.Core;

public class MainViewModelBaseTests
{
    private sealed class PageA;
    private sealed class PageB;

    // The whole of a main view model: only the entries.
    private sealed class Menu(IRouter router, bool initialize = false) : MainViewModelBase(router, initialize)
    {
        public void ViewModelBecameCurrent(object viewModel) => OnViewModelChanged(viewModel);

        protected override ObservableCollection<IRouteableEntry> GetRouteables() =>
        [
            new RouteableEntry<PageA>("A", route: "/a", template: "/a"),
            new RouteableEntry<PageB>("B", route: "/b", template: "/b"),
        ];
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

        Assert.Equal(["A", "B"], sut.Routeables.Select(r => r.DisplayName));
        Assert.Equal("Application", sut.CurrentTitle);
    }

    [Fact]
    public void Raises_property_changed_and_announces_the_title_with_the_selected_entry()
    {
        var sut = new Menu(CreateRouter());
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.CurrentView = "v";

        Assert.Contains(nameof(sut.CurrentView), changed);
    }

    [Fact]
    public void The_entry_of_the_current_view_model_is_found_by_its_type_whatever_the_route_is()
    {
        var sut = new Menu(CreateRouter());
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Routes "/a" and "/b" are not type names: the entry is the RouteableEntry<PageB>.
        sut.ViewModelBecameCurrent(new PageB());

        Assert.Same(sut.Routeables[1], sut.Routeable);
        Assert.Equal("B", sut.CurrentTitle);
        Assert.Contains(nameof(sut.CurrentTitle), changed);
    }

    [Fact]
    public void A_view_model_that_is_in_no_entry_leaves_the_selection_as_it_was()
    {
        var sut = new Menu(CreateRouter());
        sut.ViewModelBecameCurrent(new PageA());

        sut.ViewModelBecameCurrent(new object());

        Assert.Same(sut.Routeables[0], sut.Routeable);
    }
}
