using Routeables.Abstraction.Lifecycle;

namespace Routeables.Abstraction.Guards;

/// <summary>
/// A rule that applies to every route change of a router, whatever the page: the user must be signed in, a feature is switched
/// off, an update is required. Register it with <c>services.AddSingleton&lt;IRouteGuard, MyGuard&gt;()</c>; every guard that is
/// registered is asked, in the order of <see cref="Order"/> (then of registration), before the route change starts. A router resolves its
/// guards once, the first time it needs them: a guard registered as <c>Transient</c> or <c>Scoped</c> lives as long as the router.
/// </summary>
/// <remarks>
/// <para>A guard is asked once the page that is entered is known (<see cref="RouteableRoutingEventArgs.To"/>: its view model, its
/// view and its arguments, including the address in <c>Args.Route</c>) and before the page that is left is asked
/// (<see cref="IRouteable.CanLeaveAsync"/>) and before anything is initialized. The route change is the same one the
/// <see cref="IRouter.Routing"/> event announces: <see cref="RouteableRoutingEventArgs.Kind"/> tells a new page (<see cref="RouteableKind.Push"/>)
/// from Back, Forward, Pop, Root or MoveTo.</para>
/// <para>A guard runs inside the route change: it must not start a route change on the router it guards (it throws, as any hook does).
/// To send the user elsewhere, return <see cref="RouteGuardDecision.Redirect"/>.</para>
/// </remarks>
public interface IRouteGuard
{
    /// <summary>
    /// Where this guard stands among the others: the lowest value is asked first, guards with the same value in the order of
    /// registration. <c>0</c> by default.
    /// </summary>
    int Order => 0;

    /// <summary>Decides whether the route change goes on.</summary>
    Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default);
}