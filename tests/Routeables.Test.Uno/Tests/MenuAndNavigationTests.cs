using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Uno;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary>
/// <see cref="RouteableMenuItem"/> and <see cref="RouteableMenu"/> over the entries of a menu, and <see cref="RouteableNavigation"/> over a
/// <c>NavigationView</c> written in XAML: the controls follow their entries and the router, and route when the user chooses.
/// </summary>
[TestClass]
[RunsOnUIThread]
public class MenuAndNavigationTests
{
    /// <summary>An entry that tells when it changes, as the entries of a menu written with an observable base class do.</summary>
    private sealed class Entry(string name, string? description = null) : IRouteableMenuEntry, INotifyPropertyChanged
    {
        private string _name = name;
        private bool _locked;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Whether the entry is locked and its menu item should be disabled.</summary>
        public bool IsLock
        {
            get => _locked;
            set { _locked = value; Changed(nameof(IsLock)); }
        }

        /// <summary>The display name shown in the menu.</summary>
        public string DisplayName
        {
            get => _name;
            set { _name = value; Changed(nameof(DisplayName)); }
        }

        /// <inheritdoc />
        public object? ViewModel { get; set; }

        /// <inheritdoc />
        public object? View { get; set; }

        /// <inheritdoc />
        public string Route => "/" + _name.ToLowerInvariant();

        /// <inheritdoc />
        public string Template => Route;

        /// <inheritdoc />
        public IRouteableArgs Arguments { get; set; } = RouteableArgs.Create();

        /// <summary>Optional description shown under the name.</summary>
        public string? Description => description;

        /// <inheritdoc />
        public string? Path => null;

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>A menu a list can bind to.</summary>
    private sealed class Menu : IMainViewModel, INotifyPropertyChanged
    {
        private IRouteableMenuEntry? _selected;

        /// <inheritdoc />
        public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];

        /// <summary>The currently selected entry.</summary>
        public IRouteableMenuEntry? SelectedEntry
        {
            get => _selected;
            set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedEntry))); }
        }

        /// <inheritdoc />
        public object? CurrentView { get; set; }

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc />
        public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler? LockChanged { add { } remove { } }
    }

    /// <summary>Yields every descendant of <paramref name="parent"/> in depth-first order.</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    /// <summary>Returns the text of all <see cref="TextBlock"/> descendants of the root.</summary>
    private static List<string> Texts(DependencyObject root) => [.. Descendants(root).OfType<TextBlock>().Select(t => t.Text)];

    /// <summary>Finds the single <see cref="TextBlock"/> whose text equals <paramref name="text"/>.</summary>
    private static TextBlock TextOf(DependencyObject root, string text) => Descendants(root).OfType<TextBlock>().Single(t => t.Text == text);

    // ------------------------------------------------------------------------------------------------ RouteableMenuItem

    /// <summary>An item shows the name and the description of its entry.</summary>
    [TestMethod]
    public async Task An_item_shows_the_name_and_the_description_of_its_entry()
    {
        var item = await TestHost.LoadAsync(new RouteableMenuItem { Item = new Entry("Cats", "The cats of the house") });
        try
        {
            CollectionAssert.Contains(Texts(item), "Cats");
            Assert.AreEqual(Visibility.Visible, TextOf(item, "The cats of the house").Visibility);
        }
        finally { await TestHost.UnloadAsync(item); }
    }

    /// <summary>An entry without a description hides the description line.</summary>
    [TestMethod]
    public async Task An_entry_without_a_description_hides_the_line_of_the_description()
    {
        var item = await TestHost.LoadAsync(new RouteableMenuItem { Item = new Entry("Cats") });
        try
        {
            var description = Descendants(item).OfType<TextBlock>().Single(t => t.Text.Length == 0);
            Assert.AreEqual(Visibility.Collapsed, description.Visibility);
        }
        finally { await TestHost.UnloadAsync(item); }
    }

    /// <summary>A locked entry disables its item; unlocking enables it again.</summary>
    [TestMethod]
    public async Task A_locked_entry_disables_its_item_and_an_unlocked_one_enables_it_again()
    {
        var entry = new Entry("Cats") { IsLock = true };
        var item = await TestHost.LoadAsync(new RouteableMenuItem { Item = entry });
        try
        {
            Assert.IsFalse(item.IsEnabled);
            entry.IsLock = false;
            await TestHost.UntilAsync(() => item.IsEnabled);
        }
        finally { await TestHost.UnloadAsync(item); }
    }

    /// <summary>A new name appears at once, even when the change comes from another thread.</summary>
    [TestMethod]
    public async Task A_new_name_appears_at_once_even_when_it_comes_from_another_thread()
    {
        var entry = new Entry("Cats");
        var item = await TestHost.LoadAsync(new RouteableMenuItem { Item = entry });
        try
        {
            entry.DisplayName = "Kittens";
            await TestHost.UntilAsync(() => Texts(item).Contains("Kittens"));
            await Task.Run(() => entry.DisplayName = "Lions");
            await TestHost.UntilAsync(() => Texts(item).Contains("Lions"));
        }
        finally { await TestHost.UnloadAsync(item); }
    }

    /// <summary>An item that left the tree no longer follows its entry.</summary>
    [TestMethod]
    public async Task An_item_that_left_the_tree_no_longer_follows_its_entry()
    {
        var entry = new Entry("Cats");
        var item = await TestHost.LoadAsync(new RouteableMenuItem { Item = entry });
        await TestHost.UnloadAsync(item);
        entry.DisplayName = "Lions";
        await Task.Delay(100);
        CollectionAssert.Contains(Texts(item), "Cats");
        CollectionAssert.DoesNotContain(Texts(item), "Lions");
    }

    /// <summary>Replacing the entry updates the item; clearing the entry empties it and leaves it enabled.</summary>
    [TestMethod]
    public async Task Another_entry_replaces_the_first_one_and_no_entry_empties_the_item()
    {
        var first = new Entry("Cats");
        var item = await TestHost.LoadAsync(new RouteableMenuItem { Item = first });
        try
        {
            item.Item = new Entry("Dogs");
            CollectionAssert.Contains(Texts(item), "Dogs");
            first.DisplayName = "Ignored";
            await Task.Delay(50);
            CollectionAssert.DoesNotContain(Texts(item), "Ignored");
            item.Item = null;
            CollectionAssert.DoesNotContain(Texts(item), "Dogs");
            Assert.IsTrue(item.IsEnabled);
        }
        finally { await TestHost.UnloadAsync(item); }
    }

    // ------------------------------------------------------------------------------------------------ RouteableListView

    /// <summary>The list shows one item for each entry of the menu it is given.</summary>
    [TestMethod]
    public async Task The_list_shows_one_item_for_each_entry_of_the_menu_it_is_given()
    {
        var menu = new Menu();
        menu.Entries.Add(new Entry("Cats"));
        menu.Entries.Add(new Entry("Dogs"));
        var list = await TestHost.LoadAsync(new RouteableMenu { DataContext = menu });
        try
        {
            await TestHost.UntilAsync(() => Descendants(list).OfType<RouteableMenuItem>().Count() == 2);
            var names = Descendants(list).OfType<RouteableMenuItem>().Select(i => i.Item!.DisplayName).ToList();
            CollectionAssert.AreEqual(new[] { "Cats", "Dogs" }, names);
        }
        finally { await TestHost.UnloadAsync(list); }
    }

    /// <summary>Choosing an entry selects it in the menu, and the menu selection is reflected in the list.</summary>
    [TestMethod]
    public async Task Choosing_an_entry_selects_it_in_the_menu_and_the_menu_selects_it_in_the_list()
    {
        var menu = new Menu();
        var cats = new Entry("Cats");
        var dogs = new Entry("Dogs");
        menu.Entries.Add(cats);
        menu.Entries.Add(dogs);
        var list = await TestHost.LoadAsync(new RouteableMenu { DataContext = menu });
        try
        {
            list.SelectedItem = dogs;
            await TestHost.UntilAsync(() => ReferenceEquals(menu.SelectedEntry, dogs));
            menu.SelectedEntry = cats;
            await TestHost.UntilAsync(() => ReferenceEquals(list.SelectedItem, cats));
        }
        finally { await TestHost.UnloadAsync(list); }
    }

    /// <summary>An entry added to the menu is added to the list.</summary>
    [TestMethod]
    public async Task An_entry_added_to_the_menu_is_added_to_the_list()
    {
        var menu = new Menu();
        menu.Entries.Add(new Entry("Cats"));
        var list = await TestHost.LoadAsync(new RouteableMenu { DataContext = menu });
        try
        {
            menu.Entries.Add(new Entry("Dogs"));
            await TestHost.UntilAsync(() => Descendants(list).OfType<RouteableMenuItem>().Count() == 2);
        }
        finally { await TestHost.UnloadAsync(list); }
    }

    // ------------------------------------------------------------------------------------------------ RouteableNavigation

    /// <summary>Builds a <see cref="NavigationView"/> with a header and two routed items (A and B).</summary>
    private static NavigationView Navigation()
    {
        var a = new NavigationViewItem { Content = "A" };
        RouteableItem.SetRoute(a, "a");
        var b = new NavigationViewItem { Content = "B" };
        RouteableItem.SetRoute(b, "b");
        var header = new NavigationViewItemHeader { Content = "No route" };
        var view = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Left };
        view.MenuItems.Add(header);
        view.MenuItems.Add(a);
        view.MenuItems.Add(b);
        return view;
    }

    /// <summary>Asserts that <paramref name="action"/> throws an exception of type <typeparamref name="T"/>.</summary>
    private static void Refused<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        Assert.Fail($"{typeof(T).Name} was expected.");
    }

    /// <summary>Finds the menu item whose attached route equals <paramref name="route"/>.</summary>
    private static NavigationViewItem ItemOf(NavigationView view, string route)
        => view.MenuItems.OfType<NavigationViewItem>().Single(i => RouteableItem.GetRoute(i) == route);

    /// <summary>AttachRouteables refuses a null control or a null container.</summary>
    [TestMethod]
    public void The_navigation_refuses_no_control_and_no_container()
    {
        Refused<ArgumentNullException>(() => RouteableNavigation.AttachRouteables(null!, TestHost.Services));
        Refused<ArgumentNullException>(() => new NavigationView().AttachRouteables(null!));
    }

    /// <summary>The navigation content is a region; the first item is selected and its page is shown.</summary>
    [TestMethod]
    public async Task The_content_of_the_navigation_is_a_region_and_its_first_item_is_selected_and_shown()
    {
        await TestHost.ResetAsync();
        var view = await TestHost.LoadAsync(Navigation());
        using var attached = view.AttachRouteables(TestHost.Services);
        try
        {
            Assert.IsTrue(view.Content is RouteableRegion);
            await TestHost.UntilAsync(() => ReferenceEquals(view.SelectedItem, ItemOf(view, "a")));
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageAView);
        }
        finally { await TestHost.UnloadAsync(view); }
    }

    /// <summary>Choosing an item routes to its route.</summary>
    [TestMethod]
    public async Task Choosing_an_item_routes_to_its_route()
    {
        await TestHost.ResetAsync();
        var view = await TestHost.LoadAsync(Navigation());
        using var attached = view.AttachRouteables(TestHost.Services);
        try
        {
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageAView);
            view.SelectedItem = ItemOf(view, "b");
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageBView);
        }
        finally { await TestHost.UnloadAsync(view); }
    }

    /// <summary>A route reached by something else selects its item without routing again.</summary>
    [TestMethod]
    public async Task A_route_reached_by_something_else_selects_its_item_without_routing_again()
    {
        await TestHost.ResetAsync();
        var view = await TestHost.LoadAsync(Navigation());
        using var attached = view.AttachRouteables(TestHost.Services);
        try
        {
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageAView);
            await TestHost.Services.GoToAsync("/b");
            await TestHost.UntilAsync(() => ReferenceEquals(view.SelectedItem, ItemOf(view, "b")));
            Assert.AreEqual(2, TestHost.Router().History.Entries.Count);
        }
        finally { await TestHost.UnloadAsync(view); }
    }

    /// <summary>A disposed navigation no longer follows the control or the router.</summary>
    [TestMethod]
    public async Task A_disposed_navigation_no_longer_follows_the_control_and_the_router()
    {
        await TestHost.ResetAsync();
        var view = await TestHost.LoadAsync(Navigation());
        var attached = view.AttachRouteables(TestHost.Services);
        try
        {
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageAView);
            attached.Dispose();
            attached.Dispose();
            view.SelectedItem = ItemOf(view, "b");
            await Task.Delay(150);
            Assert.IsTrue(TestHost.Router().CurrentView is PageAView);
            await TestHost.Services.GoToAsync("/b");
            await Task.Delay(150);
            Assert.IsFalse(ReferenceEquals(view.SelectedItem, ItemOf(view, "b")));
        }
        finally { await TestHost.UnloadAsync(view); }
    }

    /// <summary>An item without a route is left alone when selected.</summary>
    [TestMethod]
    public async Task An_item_without_a_route_is_left_alone()
    {
        await TestHost.ResetAsync();
        var view = await TestHost.LoadAsync(Navigation());
        using var attached = view.AttachRouteables(TestHost.Services);
        try
        {
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageAView);
            var header = view.MenuItems.OfType<NavigationViewItemHeader>().Single();
            view.SelectedItem = header;
            await Task.Delay(100);
            Assert.IsTrue(TestHost.Router().CurrentView is PageAView);
        }
        finally { await TestHost.UnloadAsync(view); }
    }
}