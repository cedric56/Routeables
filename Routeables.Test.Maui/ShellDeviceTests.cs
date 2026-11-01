using System.Collections.ObjectModel;
using DeviceRunners.UITesting;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Maui;
using Routeables.Routing;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// <see cref="RouteablesShell"/> and the helpers of Shell, on the platform: the items come from the menu, a locked entry is greyed, the view of the
/// selected entry is shown in its item, and the shell follows the menu only while it has a handler.
/// </summary>
public class ShellDeviceTests
{
    // ------------------------------------------------------------------------------------------------ the fixture

    /// <summary>An entry of the menu: a name, a route, and a description if the test wants a caption.</summary>
    private sealed class Entry(string name, string? description = null) : IRouteableEntry
    {
        public bool IsLock { get; set; }
        public object? ViewModel { get; set; }
        public object? View { get; set; }
        public string Route => "/" + name.ToLowerInvariant();
        public string Template => Route;
        public IRouteableArgs Arguments { get; set; } = RouteableArgs.Create();
        public string DisplayName => name;
        public string? Description => description;
        public string? Path => null;
    }

    /// <summary>The menu of the fixture: entries, a selection, and every event a host raises, by hand.</summary>
    private sealed class Menu : IMainViewModel
    {
        private IRouteableEntry? _selected;
        private object? _view;

        public ObservableCollection<IRouteableEntry> Routeables { get; } = [];

        public IRouteableEntry? Routeable
        {
            get => _selected;
            set
            {
                _selected = value;
                Selections.Add(value);
            }
        }

        public object? CurrentView
        {
            get => _view;
            set
            {
                _view = value;
                CurrentViewChanged?.Invoke(this, value);
            }
        }

        /// <summary>Every entry that was selected, in order.</summary>
        public List<IRouteableEntry?> Selections { get; } = [];

        public event EventHandler<object?>? CurrentViewChanged;
        public event EventHandler? LockChanged;

        public int LockSubscribers => LockChanged?.GetInvocationList().Length ?? 0;
        public int ViewSubscribers => CurrentViewChanged?.GetInvocationList().Length ?? 0;

        public void NotifyLockChanged() => LockChanged?.Invoke(this, EventArgs.Empty);

        // Lock and Unlock are default members of the interface: they are reached through it.
        public void LockAll() => ((IMainViewModel)this).Lock();

        public void UnlockAll() => ((IMainViewModel)this).Unlock();

        public static Menu With(params string[] names)
        {
            var menu = new Menu();
            foreach (var name in names) menu.Routeables.Add(new Entry(name));
            return menu;
        }
    }

    /// <summary>A handler that does nothing: enough for the shell to believe it is on screen, so it follows the menu.</summary>
    private sealed class QuietHandler : IViewHandler
    {
        private IView? _view;

        public object? PlatformView => null;
        public IElement? VirtualView => _view;
        IView? IViewHandler.VirtualView => _view;
        public IMauiContext? MauiContext { get; private set; }
        public bool HasContainer { get; set; }
        public object? ContainerView => null;
        public void SetMauiContext(IMauiContext mauiContext) => MauiContext = mauiContext;
        public void SetVirtualView(IElement view) => _view = view as IView;
        public void UpdateValue(string property) { }
        public void Invoke(string command, object? args = null) { }
        public void DisconnectHandler() { }
        public Microsoft.Maui.Graphics.Size GetDesiredSize(double widthConstraint, double heightConstraint) => new Microsoft.Maui.Graphics.Size(widthConstraint, heightConstraint);
        public void PlatformArrange(Microsoft.Maui.Graphics.Rect frame) { }
    }

    private static RouteablesShell ShellOf(Menu menu)
    {
        var services = new ServiceCollection().AddSingleton<IMainViewModel>(menu).BuildServiceProvider();
        return new RouteablesShell(services);
    }

    private static RouteablesShell Followed(Menu menu)
    {
        var shell = ShellOf(menu);
        shell.Handler = new QuietHandler();
        return shell;
    }

    private static ShellContent ContentOf(Shell shell, int index) => shell.Items[index].Items[0].Items[0];

    private static ContentPage PageOf(Shell shell, int index) => (ContentPage)ContentOf(shell, index).Content;

    /// <summary>The host of the view of an entry: the content of its page, or the second row of the grid that has the caption.</summary>
    private static ContentView HostOf(Shell shell, int index) => PageOf(shell, index).Content switch
    {
        ContentView host => host,
        Grid grid => grid.Children.OfType<ContentView>().Single(),
        var other => throw new InvalidOperationException($"Unexpected content: {other}")
    };

    private static List<string> Titles(Shell shell) => [.. shell.Items.Select(i => i.Items[0].Items[0].Title)];

    // ------------------------------------------------------------------------------------------------ the items

    [UIFact]
    public void Each_entry_of_the_menu_has_an_item_with_its_name_in_the_same_order()
    {
        var shell = ShellOf(Menu.With("Home", "Cats", "Dogs"));

        Assert.Equal(["Home", "Cats", "Dogs"], Titles(shell));
        Assert.Equal(["Home", "Cats", "Dogs"], Enumerable.Range(0, 3).Select(i => PageOf(shell, i).Title));
    }

    [UIFact]
    public void The_menu_is_the_binding_context_of_the_shell()
    {
        var menu = Menu.With("Home");

        Assert.Same(menu, ShellOf(menu).BindingContext);
    }

    [UIFact]
    public void A_shell_over_an_empty_menu_has_no_item_and_selects_nothing()
    {
        var menu = new Menu();

        var shell = ShellOf(menu);

        Assert.Empty(shell.Items);
        Assert.Empty(menu.Selections);
    }

    [UIFact]
    public void The_first_entry_is_selected_as_soon_as_the_shell_is_built_and_once()
    {
        var menu = Menu.With("Home", "Cats");

        _ = ShellOf(menu);

        Assert.Same(menu.Routeables[0], menu.Routeable);
        Assert.Single(menu.Selections);
    }

    [UIFact]
    public void An_entry_that_is_already_selected_is_kept()
    {
        var menu = Menu.With("Home", "Cats");
        menu.Routeable = menu.Routeables[1];
        menu.Selections.Clear();

        _ = ShellOf(menu);

        Assert.Same(menu.Routeables[1], menu.Routeable);
        Assert.Empty(menu.Selections);
    }

    [UIFact]
    public void An_entry_without_a_description_shows_its_view_directly_in_the_page()
    {
        var shell = ShellOf(Menu.With("Home"));

        Assert.IsType<ContentView>(PageOf(shell, 0).Content);
    }

    [UIFact]
    public void The_description_of_an_entry_is_a_caption_above_its_view()
    {
        var menu = new Menu();
        menu.Routeables.Add(new Entry("Home", "The start page"));

        var shell = ShellOf(menu);

        var grid = Assert.IsType<Grid>(PageOf(shell, 0).Content);
        Assert.Equal("The start page", grid.Children.OfType<Label>().Single().Text);
        Assert.Single(grid.Children.OfType<ContentView>());
    }

    // ------------------------------------------------------------------------------------------------ the entries that arrive

    [UIFact]
    public void An_entry_added_before_the_shell_has_a_handler_gets_its_item_when_the_handler_comes()
    {
        var menu = Menu.With("Home");
        var shell = ShellOf(menu);

        menu.Routeables.Add(new Entry("Late"));
        Assert.Single(shell.Items);

        shell.Handler = new QuietHandler();

        Assert.Equal(["Home", "Late"], Titles(shell));
    }

    [UIFact]
    public void An_entry_added_while_the_shell_is_followed_gets_its_item()
    {
        var menu = Menu.With("Home");
        var shell = Followed(menu);

        menu.Routeables.Add(new Entry("Cats"));

        Assert.Equal(["Home", "Cats"], Titles(shell));
    }

    [UIFact]
    public void An_entry_inserted_in_the_middle_gets_its_item_at_the_same_place()
    {
        var menu = Menu.With("Home", "Dogs");
        var shell = Followed(menu);

        menu.Routeables.Insert(1, new Entry("Cats"));

        Assert.Equal(["Home", "Cats", "Dogs"], Titles(shell));
    }

    [UIFact]
    public void Several_entries_added_together_each_get_their_item()
    {
        var menu = Menu.With("Home");
        var shell = Followed(menu);

        foreach (var name in new[] { "Cats", "Dogs", "Birds" })
            menu.Routeables.Add(new Entry(name));

        Assert.Equal(["Home", "Cats", "Dogs", "Birds"], Titles(shell));
    }

    [UIFact]
    public void The_other_changes_of_the_menu_are_not_followed()
    {
        var menu = Menu.With("Home", "Cats");
        var shell = Followed(menu);

        menu.Routeables.RemoveAt(1);

        Assert.Equal(["Home", "Cats"], Titles(shell));
    }

    // ------------------------------------------------------------------------------------------------ the locks

    [UIFact]
    public void Locking_the_menu_greys_every_item_and_unlocking_enables_them_again()
    {
        var menu = Menu.With("Home", "Cats");
        var shell = Followed(menu);

        menu.LockAll();

        for (var i = 0; i < 2; i++)
        {
            Assert.False(shell.Items[i].IsEnabled);
            Assert.False(shell.Items[i].Items[0].IsEnabled);
            Assert.False(ContentOf(shell, i).IsEnabled);
        }

        menu.UnlockAll();

        for (var i = 0; i < 2; i++)
        {
            Assert.True(shell.Items[i].IsEnabled);
            Assert.True(shell.Items[i].Items[0].IsEnabled);
            Assert.True(ContentOf(shell, i).IsEnabled);
        }
    }

    [UIFact]
    public void An_entry_locked_before_the_shell_has_a_handler_is_greyed_when_the_handler_comes()
    {
        var menu = Menu.With("Home", "Cats");
        var shell = ShellOf(menu);
        menu.Routeables[1].IsLock = true;

        shell.Handler = new QuietHandler();

        Assert.True(shell.Items[0].IsEnabled);
        Assert.False(shell.Items[1].IsEnabled);
    }

    [UIFact]
    public void An_item_added_while_the_menu_is_locked_is_greyed_at_the_next_change_of_the_locks()
    {
        var menu = Menu.With("Home");
        var shell = Followed(menu);
        menu.LockAll();

        menu.Routeables.Add(new Entry("Late") { IsLock = true });
        menu.NotifyLockChanged();

        Assert.False(shell.Items[1].IsEnabled);
    }

    // ------------------------------------------------------------------------------------------------ the view

    [UIFact]
    public void The_view_of_the_selected_entry_is_shown_in_its_item_and_replaced_by_the_next_one()
    {
        var menu = Menu.With("Home", "Cats");
        var shell = Followed(menu);
        var first = new Label { Text = "first" };
        var second = new Label { Text = "second" };

        menu.CurrentView = first;
        Assert.Same(first, HostOf(shell, 0).Content);
        Assert.Null(HostOf(shell, 1).Content);

        menu.CurrentView = second;
        Assert.Same(second, HostOf(shell, 0).Content);
    }

    [UIFact]
    public void The_view_goes_to_the_item_of_the_entry_the_menu_shows()
    {
        var menu = Menu.With("Home", "Cats");
        var shell = Followed(menu);
        menu.Routeable = menu.Routeables[1];
        var view = new Label { Text = "cats" };

        menu.CurrentView = view;

        Assert.Same(view, HostOf(shell, 1).Content);
        Assert.Null(HostOf(shell, 0).Content);
    }

    [UIFact]
    public void A_view_produced_before_the_shell_had_a_handler_is_shown_when_the_handler_comes()
    {
        var menu = Menu.With("Home");
        var shell = ShellOf(menu);
        var view = new Label { Text = "early" };
        menu.CurrentView = view;
        Assert.Null(HostOf(shell, 0).Content);

        shell.Handler = new QuietHandler();

        Assert.Same(view, HostOf(shell, 0).Content);
    }

    [UIFact]
    public void A_view_that_is_not_a_control_shows_nothing()
    {
        var menu = Menu.With("Home");
        var shell = Followed(menu);

        menu.CurrentView = new object();

        Assert.Null(HostOf(shell, 0).Content);
    }

    [UIFact]
    public async Task A_view_announced_from_another_thread_is_shown_on_the_thread_of_the_interface()
    {
        var menu = Menu.With("Home");
        var shell = Followed(menu);
        var view = new Label { Text = "background" };

        await Task.Run(() => menu.CurrentView = view);

        await Device.UntilAsync(() => ReferenceEquals(HostOf(shell, 0).Content, view));
    }

    // ------------------------------------------------------------------------------------------------ letting go

    [UIFact]
    public void The_shell_follows_the_menu_once_per_handler_and_lets_go_with_it()
    {
        var menu = Menu.With("Home");
        var shell = ShellOf(menu);
        Assert.Equal(0, menu.LockSubscribers);

        shell.Handler = new QuietHandler();
        Assert.Equal(1, menu.LockSubscribers);
        Assert.Equal(1, menu.ViewSubscribers);

        shell.Handler = new QuietHandler();
        Assert.Equal(1, menu.LockSubscribers);
        Assert.Equal(1, menu.ViewSubscribers);

        shell.Handler = null;
        Assert.Equal(0, menu.LockSubscribers);
        Assert.Equal(0, menu.ViewSubscribers);
    }

    [UIFact]
    public void A_shell_that_lost_its_handler_no_longer_changes_with_the_menu()
    {
        var menu = Menu.With("Home");
        var shell = Followed(menu);
        shell.Handler = null;

        menu.Routeables.Add(new Entry("Late"));
        menu.LockAll();
        menu.CurrentView = new Label();

        Assert.Single(shell.Items);
        Assert.True(shell.Items[0].IsEnabled);
        Assert.Null(HostOf(shell, 0).Content);
    }

    // ------------------------------------------------------------------------------------------------ the helpers of Shell

    [UIFact]
    public void The_routes_of_the_shell_refuse_no_shell_and_no_container()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => ShellRoutes.UseShellRoutes(null!, services));
        Assert.Throws<ArgumentNullException>(() => ShellRoutes.UseShellRoutes(new Shell(), null!));
    }

    [UIFact]
    public void Following_the_routes_of_the_shell_stops_when_disposed_and_disposing_twice_is_harmless()
    {
        var subscription = new Shell().UseShellRoutes(new ServiceCollection().BuildServiceProvider());

        subscription.Dispose();
        subscription.Dispose();
    }

    [UIFact]
    public void The_items_of_a_shell_refuse_no_shell_and_no_container()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => ShellItems.AttachRouteables(null!, services));
        Assert.Throws<ArgumentNullException>(() => new Shell().AttachRouteables(null!));
    }

    [UIFact]
    public void The_content_of_a_shell_content_that_has_a_route_is_a_region_page_and_the_others_are_left_to_Shell()
    {
        var shell = new Shell();
        var routed = new ShellContent { Title = "A", Route = "a" };
        var own = new ShellContent { Title = "Own", Route = "own", Content = new ContentPage() };
        var unrouted = new ShellContent { Title = "None" };
        // The hierarchy is written out: adding a ShellContent straight to the shell wraps it in items that Shell names itself.
        var section = new ShellSection { Title = "Section", Route = "section" };
        section.Items.Add(routed);
        section.Items.Add(own);
        section.Items.Add(unrouted);
        var item = new ShellItem { Title = "Item", Route = "item" };
        item.Items.Add(section);
        shell.Items.Add(item);

        using var attached = shell.AttachRouteables(Device.Services);

        Assert.NotNull(routed.ContentTemplate);
        Assert.Null(own.ContentTemplate);
        Assert.Null(unrouted.ContentTemplate);
    }
}
