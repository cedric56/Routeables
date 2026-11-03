using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui;

/// <summary>
/// A <see cref="Shell"/> whose top-level items (tabs or flyout entries) come from <see cref="IMainViewModel.Routeables"/>.
/// Shell owns the top-level structure; Routeables owns the route change inside each item, so pages managed
/// by the router must not also be routed with <c>Shell.GoToAsync</c>.
/// </summary>
public partial class RouteablesShell : Shell
{
    private readonly IServiceProvider services;
    private readonly IMainViewModel mainViewModel;
    private readonly List<ContentView> hosts = [];
    private bool started;

    /// <summary>Creates the shell: its items come from the main view model registered in <paramref name="provider"/>.</summary>
    /// <param name="provider">The container of the application.</param>
    public RouteablesShell(IServiceProvider provider)
    {
        InitializeComponent();

        services = provider;
        mainViewModel = provider.GetRequiredService<IMainViewModel>();
        BindingContext = mainViewModel;

        foreach (var routeable in mainViewModel.Routeables)
            AddItem(routeable, hosts.Count);

        // The content of the first item is resolved from the start: its route change begins now, not when the shell appears.
        mainViewModel.SelectFirst();
    }

    // An item for an entry, at its place in the collection: the view produced by the router is shown inside its host.
    private void AddItem(IRouteableEntry routeable, int index)
    {
        var host = new ContentView();
        index = Math.Clamp(index, 0, hosts.Count);
        hosts.Insert(index, host);

        ShellItem item = new ShellContent
        {
            Title = routeable.DisplayName,
            Icon = routeable.Source as ImageSource,
            Content = BuildPage(routeable, host)
        };
        Items.Insert(index, item);

        // The entry shown keeps its item: an item put before it moves its index.
        if (shown >= index)
            shown++;
    }

    // An entry added while the application runs (a module loaded on demand, IRouteableModules.Load) gets its item at its place in the
    // collection (AddMenuEntries chooses it). Other changes of the collection are not followed.
    private void OnRouteablesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => OnUiThread(() =>
    {
        if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add || e.NewItems is null) return;
        var index = e.NewStartingIndex >= 0 ? e.NewStartingIndex : mainViewModel.Routeables.Count - e.NewItems.Count;
        foreach (IRouteableEntry routeable in e.NewItems)
            AddItem(routeable, index++);
    });

    // A locked entry (see ILockCoordinator) is greyed in the flyout and the tabs. Which of the item, its section or its content a
    // platform reads depends on how it draws them (a ShellContent put in Items is wrapped in an item and a section), so all three are set.
    private void OnLockChanged(object? sender, EventArgs e) => OnUiThread(() =>
    {
        for (var i = 0; i < Items.Count && i < mainViewModel.Routeables.Count; i++)
        {
            var enabled = !mainViewModel.Routeables[i].IsLock;
            Items[i].IsEnabled = enabled;
            foreach (var section in Items[i].Items)
            {
                section.IsEnabled = enabled;
                foreach (var content in section.Items)
                    content.IsEnabled = enabled;
            }
        }
    });

    // Routeables -> Shell: the current view is displayed in the active item.
    private void OnCurrentViewChanged(object? sender, object? view) => OnUiThread(() =>
    {
        var index = ShownIndex();
        if (index >= 0 && index < hosts.Count)
            hosts[index].Content = view as View;
    });

    // The entry the view model shows; before Shell has a current item (it has none until it appears) the shell cannot tell it.
    private int ShownIndex()
    {
        var index = mainViewModel.Routeable is { } routeable ? mainViewModel.Routeables.IndexOf(routeable) : -1;
        return index >= 0 ? index : CurrentIndex();
    }

    // A view the router produced before the shell listened, or before it had a current item, is put in its host.
    private void ShowCurrent() => OnUiThread(() =>
    {
        var index = ShownIndex();
        if (index >= 0 && index < hosts.Count && mainViewModel.CurrentView is View view && !ReferenceEquals(hosts[index].Content, view))
            hosts[index].Content = view;
    });

    // A view model may raise its events from any thread; the controls only accept the one of the dispatcher.
    private void OnUiThread(Action action)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(action);
        else
            action();
    }

    private bool following;

    /// <inheritdoc />
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // Subscribe once per handler, and let go when the handler goes away, so a recreated shell does not leave
        // a dead one listening (and updating its controls) for the rest of the application's life.
        if (args.NewHandler is null && following)
        {
            mainViewModel.LockChanged -= OnLockChanged;
            mainViewModel.CurrentViewChanged -= OnCurrentViewChanged;
            mainViewModel.Routeables.CollectionChanged -= OnRouteablesChanged;
            following = false;
        }
        else if (args.NewHandler is not null && !following)
        {
            mainViewModel.LockChanged += OnLockChanged;
            mainViewModel.CurrentViewChanged += OnCurrentViewChanged;
            mainViewModel.Routeables.CollectionChanged += OnRouteablesChanged;
            following = true;
            SyncItems();                              // the entries added before the shell had a handler
            OnLockChanged(this, EventArgs.Empty);     // the locks set before the shell had a handler
            ShowCurrent();                            // the view of the first entry, if its navigation ended before
        }
    }

    // The Back button or gesture of the system: the shell first (it closes its flyout), then one page back in the router.
    // At the first page nothing is handled, and Android leaves the application.
    /// <inheritdoc />
    protected override bool OnBackButtonPressed() => base.OnBackButtonPressed() || RouteableBack.TryGoBack(services);

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Shell does not report the initially selected item as a route change, so activate it once.
        if (started) return;
        started = true;
        Activate();
        ShowCurrent();
    }

    // Shell -> Routeables: selecting another item tells the view model.
    /// <inheritdoc />
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (started)
            Activate();
    }

    // The description (if any) is shown as a caption above the view, since a flyout item has no subtitle.
    // The entries added between the constructor and the handler (a module loaded in that time) are in the collection but not in the shell.
    private void SyncItems()
    {
        if (hosts.Count == mainViewModel.Routeables.Count) return;

        Items.Clear();
        hosts.Clear();
        shown = -1;
        foreach (var routeable in mainViewModel.Routeables)
            AddItem(routeable, hosts.Count);
    }

    private static ContentPage BuildPage(IRouteableEntry routeable, ContentView host)
    {
        if (string.IsNullOrWhiteSpace(routeable.Description))
            return new ContentPage { Title = routeable.DisplayName, Content = host };

        var caption = new Label
        {
            Text = routeable.Description,
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(16, 8, 16, 4)
        };
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        grid.Add(caption, 0, 0);
        grid.Add(host, 0, 1);
        return new ContentPage { Title = routeable.DisplayName, Content = grid };
    }

    private int CurrentIndex() => CurrentItem is null ? -1 : Items.IndexOf(CurrentItem);

    // The index of the entry that is shown; -1 before the first one.
    private int shown = -1;

    private void Activate()
    {
        var index = CurrentIndex();
        if (index < 0 || index >= mainViewModel.Routeables.Count) return;

        var routeable = mainViewModel.Routeables[index];

        // A locked entry cannot be entered, whatever the platform does with IsEnabled: the selection goes back to the entry shown.
        if (shown >= 0 && index != shown && routeable.IsLock)
        {
            CurrentItem = Items[shown];
            return;
        }

        shown = index;
        if (ReferenceEquals(mainViewModel.Routeable, routeable)) return;

        // The Routeable setter of the view model starts the route change (see MainViewModel in the sample).
        mainViewModel.Routeable = routeable;
    }
}