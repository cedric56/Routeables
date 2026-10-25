using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Services;
using Windows.UI.Core;

namespace Routeables.Uno;

// The Back request of the system (the hardware button or gesture of Android, the back button of a title bar), answered by the router.
// It is listened to only while the router has a page to go back to: on Android 16 and later, whoever listens consumes the request, and the
// application could not be left from its first page.
internal sealed class BackRequests : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly IRouter? _router;
    private readonly IMainRouteables? _menu;
    private readonly HashSet<IRouter> _followed = [];
    private bool _listening;

    public BackRequests(IServiceProvider provider)
    {
        _provider = provider;
        _router = provider.GetService<IRouter>();
        if (_router is not null)
            Follow(_router);

        // A tab with its own history (an entry with a region) answers Back first: its router is followed once the tab is selected.
        _menu = provider.GetService<IMainRouteables>();
        if (_menu is not null)
            _menu.CurrentViewChanged += OnSelectionChanged;

        Update();
    }

    private void Follow(IRouter router)
    {
        if (_followed.Add(router))
            router.History.Changed += OnHistoryChanged;
    }

    private void OnSelectionChanged(object? sender, object? view) => OnHistoryChanged(sender, EventArgs.Empty);

    // The history may change on any thread; the manager belongs to the one of the interface.
    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        var dispatcher = _provider.GetService<IRouteableDispatcher>();
        if (dispatcher is null)
            Update();
        else
            dispatcher.Post(Update);
    }

    private void Update()
    {
        if (_menu?.Routeable?.Region is { Length: > 0 } region)
        {
            try
            {
                Follow(_provider.GetRouter(region));
            }
            catch (InvalidOperationException)
            {
                // A region that is not registered has no history of its own.
            }
        }

        var wanted = RouteableBack.CanGoBack(_provider);
        if (wanted == _listening)
            return;

        try
        {
            var manager = SystemNavigationManager.GetForCurrentView();
            if (wanted)
                manager.BackRequested += OnBackRequested;
            else
                manager.BackRequested -= OnBackRequested;

            _listening = wanted;
        }
        catch (Exception)
        {
            // A platform that has no such manager (a desktop head without a title bar back button): the system keeps its Back.
        }
    }

    private void OnBackRequested(object? sender, BackRequestedEventArgs e)
    {
        if (RouteableBack.TryGoBack(_provider))
            e.Handled = true;
    }

    public void Dispose()
    {
        foreach (var router in _followed)
            router.History.Changed -= OnHistoryChanged;
        _followed.Clear();

        if (_menu is not null)
            _menu.CurrentViewChanged -= OnSelectionChanged;

        try
        {
            if (_listening)
                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
        }
        catch (Exception)
        {
            // Nothing to let go of.
        }

        _listening = false;
    }
}
