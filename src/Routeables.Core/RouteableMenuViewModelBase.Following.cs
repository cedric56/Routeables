using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Routeables.Core;

// How the menu follows a page the router showed without it: a Back, a link, a guard's redirection.
public abstract partial class RouteableMenuViewModelBase
{
    /// <summary>
    /// Follows the router: when a page is shown by something other than a selection, selects its entry
    /// and shows its view.
    /// </summary>
    /// <param name="viewModel">The view model that is now current.</param>
    protected override void OnViewModelChanged(object? viewModel)
    {
        if (viewModel == null)
            return;

        _followed = viewModel;
        _followedRoute = _router.History.Current?.Args.Route;

        // The entry for that page: the one whose route is the route of the page shown (several entries can show the same type with different
        // routes: /products/shoes and /products/hats). Otherwise the entry whose route is the type name, or a RouteableMenuEntry<T> of that type.
        var type = viewModel.GetType();
        IRouteableMenuEntry? entry = null;
        if (ReferenceEquals(viewModel, _router.CurrentViewModel) && _router.History.Current?.Args.Route is { } shownRoute)
            entry = Entries.FirstOrDefault(p => RouteAddress.SameFor(_router, p.Arguments.Route ?? p.Route, shownRoute));
        entry ??= Entries.FirstOrDefault(p => p.Route == type.Name || (p as RouteableMenuEntry)?.ViewModelType == type);
        if (entry != null)
            _routeable = _confirmed = entry;
        NotifyPropertyChanged(nameof(SelectedEntry));
        NotifyPropertyChanged(nameof(CurrentTitle));
        CurrentViewChanged?.Invoke(this, _router.CurrentView);
        base.OnViewModelChanged(viewModel);
    }

    // A page that moves the router itself (its own Back or Pop button, for instance) does not tell the menu; the router does.
    // A selection made by the menu, and a move the menu has already followed, are not announced twice.
    private object? _followed;
    // The route of the page that was followed: one cached view model can serve two routes, and moving from one to the other is a move.
    private string? _followedRoute;

    private bool Follows(object? viewModel)
        => ReferenceEquals(viewModel, _followed)
           && (_router.History.Current?.Args.Route is not { } route ? _followedRoute is null : RouteAddress.SameFor(_router, route, _followedRoute));

    // Greater than 0 while this menu's GoBackAsync and GoForwardAsync run: they follow the router themselves once the move is done.
    // It is a count, not a flag: when two of them overlap (a double click), the first to finish must not switch it off for the other.
    private int _selfMoving;

    // Greater than 0 while a selection of this menu is asking the router for its page.
    private int _selecting;

    /// <inheritdoc />
    public override async Task GoBackAsync()
    {
        Interlocked.Increment(ref _selfMoving);
        try { await base.GoBackAsync(); }
        finally { Interlocked.Decrement(ref _selfMoving); }
    }

    /// <inheritdoc />
    public override async Task GoForwardAsync()
    {
        Interlocked.Increment(ref _selfMoving);
        try { await base.GoForwardAsync(); }
        finally { Interlocked.Decrement(ref _selfMoving); }
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        // A Push that the menu asked for (a selection) is announced by the selection itself. Any other one (router.GoToAsync, a RouteableLink, a
        // guard's redirection) is a page the menu did not choose, so it follows it like a Back.
        if (Volatile.Read(ref _selfMoving) > 0 || !e.Succeeded || (e.Kind == RouteableKind.Push && _selecting > 0) || Follows(_router.CurrentViewModel))
            return;

        OnRouterMoved(_router.CurrentViewModel);
    }

    /// <inheritdoc />
    public void OnRouterMoved(object? viewModel)
    {
        _followed = viewModel;
        OnViewModelChanged(viewModel);
    }
}
