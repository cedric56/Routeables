using Routeables.Abstraction;

namespace Routeables.Maui;

/// <summary>
/// A <see cref="Shell"/> whose top-level items (tabs or flyout entries) come from <see cref="IMainRouteables.Routeables"/>.
/// Shell owns the top-level structure; Routeables owns the route change inside each item, so pages managed
/// by the router must not also be routed with <c>Shell.GoToAsync</c>.
/// </summary>
public partial class RouteablesShell : Shell
{
    private readonly IMainRouteables vm;
    private readonly List<ContentView> hosts = [];
    private bool started;

    public RouteablesShell(IServiceProvider provider)
    {
        InitializeComponent();

        vm = provider.GetRequiredService<IMainRouteables>();
        BindingContext = vm;

        foreach (var routeable in vm.Routeables)
        {
            // The view produced by the router is shown inside this host.
            var host = new ContentView();
            hosts.Add(host);

            Items.Add(new ShellContent
            {
                Title = routeable.DisplayName,
                Icon = routeable.Source as ImageSource,
                Content = BuildPage(routeable, host)
            });
        }

        // The content of the first item is resolved from the start: its route change begins now, not when the shell appears.
        vm.SelectFirst();
    }

    // A locked entry (see ILockCoordinator) is greyed in the flyout and the tabs. Which of the item, its section or its content a
    // platform reads depends on how it draws them (a ShellContent put in Items is wrapped in an item and a section), so all three are set.
    private void OnLockChanged(object? sender, EventArgs e) => OnUiThread(() =>
    {
        for (var i = 0; i < Items.Count && i < vm.Routeables.Count; i++)
        {
            var enabled = !vm.Routeables[i].IsLock;
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
        var index = vm.Routeable is { } routeable ? vm.Routeables.IndexOf(routeable) : -1;
        return index >= 0 ? index : CurrentIndex();
    }

    // A view the router produced before the shell listened, or before it had a current item, is put in its host.
    private void ShowCurrent() => OnUiThread(() =>
    {
        var index = ShownIndex();
        if (index >= 0 && index < hosts.Count && vm.CurrentView is View view && !ReferenceEquals(hosts[index].Content, view))
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

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // Subscribe once per handler, and let go when the handler goes away, so a recreated shell does not leave
        // a dead one listening (and updating its controls) for the rest of the application's life.
        if (args.NewHandler is null && following)
        {
            vm.LockChanged -= OnLockChanged;
            vm.CurrentViewChanged -= OnCurrentViewChanged;
            following = false;
        }
        else if (args.NewHandler is not null && !following)
        {
            vm.LockChanged += OnLockChanged;
            vm.CurrentViewChanged += OnCurrentViewChanged;
            following = true;
            OnLockChanged(this, EventArgs.Empty);     // the locks set before the shell had a handler
            ShowCurrent();                            // the view of the first entry, if its navigation ended before
        }
    }

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
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (started)
            Activate();
    }

    // The description (if any) is shown as a caption above the view, since a flyout item has no subtitle.
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
        if (index < 0 || index >= vm.Routeables.Count) return;

        var routeable = vm.Routeables[index];

        // A locked entry cannot be entered, whatever the platform does with IsEnabled: the selection goes back to the entry shown.
        if (shown >= 0 && index != shown && routeable.IsLock)
        {
            CurrentItem = Items[shown];
            return;
        }

        shown = index;
        if (ReferenceEquals(vm.Routeable, routeable)) return;

        // The Routeable setter of the view model starts the route change (see MainViewModel in the sample).
        vm.Routeable = routeable;
    }
}