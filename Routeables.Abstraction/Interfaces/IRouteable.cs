namespace Routeables.Abstraction;

/// <summary>
/// The lifecycle of a page, all of it optional: a view model implements the members it needs, and the router calls them around a
/// routing. In order: <see cref="CanLeaveAsync"/> on the page being left (it may refuse), then <see cref="OnLeavingAsync"/>
/// and <see cref="OnEnteringAsync"/> before the change, and <see cref="OnLeftAsync"/> and <see cref="OnEnteredAsync"/>
/// once it is committed to the history.
/// </summary>
public interface IRouteable
{
    /// <summary>The guard: asked of the page that is being left. Return <see langword="false"/> to refuse, and the route change is cancelled.</summary>
    /// <param name="ct">A token to observe for cancellation.</param>
    Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);

    /// <summary>Called on the target before the route change is committed; the page can prepare itself. An exception here fails the route change.</summary>
    /// <param name="context">The arguments of the route change.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>Called on the page being left, before the route change is committed. An exception here fails the route change.</summary>
    /// <param name="context">The arguments of the route change.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Called on the entry that was left, once the route change is committed to the history (Prism's <c>OnLeft</c>).
    /// Unlike <see cref="OnLeavingAsync"/>, it cannot fail or veto the route change: an exception thrown here is
    /// swallowed, because the history has already moved. It runs before the entries that left the history are disposed.
    /// </summary>
    Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Called on the entry that is now current, once the route change is committed to the history (Prism's <c>OnEntered</c>),
    /// after <see cref="OnLeftAsync"/> of the entry that was left. Same rules: no veto, exceptions are swallowed.
    /// </summary>
    Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
}