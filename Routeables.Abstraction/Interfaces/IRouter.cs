namespace Routeables.Abstraction;

/// <summary>
/// The route change of an application, or of one region of it: it keeps a history of entries (a view model and its view) and moves a
/// cursor through it. Every operation checks the guards of the page being left, runs the lifecycle (<see cref="IRouteable"/>),
/// and returns a <see cref="RouteableResult"/> instead of throwing when the route change is refused or fails. The default router is
/// the main area; <c>AddRegion</c> registers one more, by name, for a <c>RouteableRegion</c>.
/// </summary>
public interface IRouter
{
    /// <summary>The history of this router: its entries and the cursor.</summary>
    IRouterHistory History { get; }

    /// <summary>
    /// Raised once a route change attempt is under way (target known, before any guard or lifecycle callback).
    /// Not raised when there is nothing to do (no back entry, already at the target).
    /// Raised while the router gate is held: handlers must not start a route change.
    /// </summary>
    event EventHandler<RouteableRoutingEventArgs>? Routing;

    /// <summary>Raised exactly once after every <see cref="Routing"/>, whatever the outcome.</summary>
    event EventHandler<RouteableRoutedEventArgs>? Routed;

    /// <summary>Whether <see cref="GoBackAsync"/> has an entry to go back to.</summary>
    bool CanGoBack { get; }

    /// <summary>Whether <see cref="ForwardAsync"/> has an entry to go forward to.</summary>
    bool CanGoForward { get; }

    /// <summary>The view model of the current entry. Use <c>GetCurrentViewModel&lt;T&gt;()</c> for a typed one.</summary>
    object? CurrentViewModel { get; }

    /// <summary>The view of the current entry, or <see langword="null"/> when the history is empty.</summary>
    object? CurrentView { get; }

    /// <summary>
    /// Moves the cursor back one entry and keeps the entry it left, so <see cref="ForwardAsync"/> returns to it
    /// (browser-style history). To leave a page for good, such as a detail opened from a list, use <see cref="PopAsync"/>.
    /// </summary>
    Task<RouteableResult> GoBackAsync(CancellationToken cancellationToken = default);

    /// <summary>Moves the cursor forward one entry. Only entries left with <see cref="GoBackAsync"/> can be reached.</summary>
    Task<RouteableResult> ForwardAsync(CancellationToken cancellationToken = default);

    /// <summary>Go to the first entry and discard every entry after it.</summary>
    Task<RouteableResult> GoBackToRootAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Go back one entry and discard the entry (and any forward entries) that was left: the way out of a drill-down
    /// page (a detail, a dialog-like step) that must not stay in the history.
    /// </summary>
    Task<RouteableResult> PopAsync(CancellationToken cancellationToken = default);

    /// <summary>Jump to the entry at <paramref name="index"/> (see IRouterHistory.Entries), keeping all entries.</summary>
    Task<RouteableResult> MoveToAsync(int index, CancellationToken cancellationToken = default);

    /// <summary>Forgets every entry, disposing the view models that are disposable. The router is then empty until the next route change.</summary>
    Task EraseHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Goes to a registered route by its address, as a browser would: <c>GoToAsync("/orders/42")</c>. The route is looked up in
    /// the routes registered with an explicit route (<c>RouteableTable</c>) and in the main routeables; its parameters
    /// (<c>{id:int}</c>, query) are read from <paramref name="url"/>. The leading <c>/</c> is optional. Fails (without throwing) when no route matches.
    /// </summary>
    /// <param name="url">The address to go to.</param>
    /// <param name="args">The arguments of the route change, or <see langword="null"/> for none.</param>
    /// <param name="template">
    /// The registered route to go to, when the address alone does not tell it (<c>"/orders/{id:int}"</c>); found from the
    /// address when omitted. A template that is not registered is used as it is.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<RouteableResult> GoToAsync(string url, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Goes to a view and a view model you built yourself, adding them to the history. The router does not create anything:
    /// it binds the view to the view model (<see cref="IRouteableFor.Context"/>) and runs the lifecycle.
    /// </summary>
    /// <param name="view">The view to show.</param>
    /// <param name="viewModel">The view model of that view.</param>
    /// <param name="args">The arguments of the route change, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<RouteableResult> GoAsync(object view, object viewModel, IRouteableArgs? args = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Goes to <paramref name="route"/> with <typeparamref name="TViewModel"/> as the view model: <c>GoToRouteAsync&lt;MenuViewModel&gt;("MenuView")</c>.
    /// The view is the one registered under the route, the view model is built from <typeparamref name="TViewModel"/>. The
    /// <paramref name="template"/> is optional: without it, it is the one registered for the route (<c>RouteableTable</c>, then the
    /// main routeables), or the route itself.
    /// </summary>
    /// <typeparam name="TViewModel">The type of the view model to build.</typeparam>
    /// <param name="route">The route to go to, with its values (<c>"/orders/42"</c>).</param>
    /// <param name="args">The arguments of the route change, or <see langword="null"/> for none.</param>
    /// <param name="template">The registered template, when the route alone does not tell it.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Keys that tell views of the same type apart in the cache.</param>
    Task<RouteableResult> GoToRouteAsync<TViewModel>(
        string route,
        IRouteableArgs? args = null,
        string? template = null,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
        where TViewModel : class;

    /// <summary>Goes to a menu entry, with <typeparamref name="TViewModel"/> as the view model.</summary>
    /// <typeparam name="TViewModel">The type of the view model to build.</typeparam>
    /// <param name="routeableView">The entry to go to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Keys that tell views of the same type apart in the cache.</param>
    Task<RouteableResult> GoToRouteAsync<TViewModel>(
        IRouteableEntry routeableView, 
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
        where TViewModel : class;

    /// <summary>Goes to a menu entry, which knows its route, its template and its view model.</summary>
    /// <param name="routeableView">The entry to go to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Keys that tell views of the same type apart in the cache.</param>
    Task<RouteableResult> GoToRouteAsync(
        IRouteableEntry routeableView,
        CancellationToken cancellationToken = default,
        params string[] viewKeys);
}