using Routeables.Abstraction.Routing;

namespace Routeables.Abstraction.Guards;

/// <summary>
/// A rule that is asked about a page <b>before it is built</b>: from the type of its view model and the address, with no instance. An
/// <see cref="IRouteGuard"/> is asked once the page exists (its constructor has run, its dependencies were injected), so a visitor who is refused still
/// causes whatever a constructor does (a load, a call to a service, a log). A type guard is for the rules that the type alone settles, such as
/// <c>[Authorize]</c>: the page of a visitor who is refused is never built. The guards of both kinds are asked: the type guards first, for a new page.
/// </summary>
/// <remarks>
/// <para>Register it with <c>services.AddSingleton&lt;IRouteTypeGuard, MyGuard&gt;()</c>. Only the pages that a route change builds are asked: one that is
/// shown again from the history, or from the entry of a menu that remembers it, was built (and asked) before. The view is not built yet, so a rule that
/// reads the view is for <see cref="IRouteGuard"/>. A rule that depends on who the visitor is now (signed in or not) must also be an <see cref="IRouteGuard"/>: it is asked at every route change, a page that is shown again included.</para>
/// <para>It runs before the route change starts: it is not announced by <see cref="IRouter.Routing"/>, and a decision to cancel is a route change that
/// did not start. <see cref="RouteGuardDecision.Redirect"/> sends the user elsewhere.</para>
/// </remarks>
public interface IRouteTypeGuard
{
    /// <summary>Where this guard stands among the others: the lowest value is asked first. <c>0</c> by default.</summary>
    int Order => 0;

    /// <summary>Decides whether a page of this view model type may be built for this address.</summary>
    /// <param name="viewModelType">The type of the view model the route is registered with.</param>
    /// <param name="route">The address that was asked for (<c>/orders/42</c>).</param>
    /// <param name="cancellationToken">The cancellation of the route change.</param>
    Task<RouteGuardDecision> CanBuildAsync(Type viewModelType, string route, CancellationToken cancellationToken = default);
}
