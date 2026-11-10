using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;

namespace Routeables.Abstraction.Locking;

/// <summary>
/// What a router offers to a menu view model: the coordinator that locks the menu while a page blocks leaving. Separate from
/// <see cref="IRouter"/>, which is about routing; <c>router.CreateLockCoordinator(menu)</c> asks it.
/// </summary>
internal interface IRouteableLockCoordinatorFactory
{
    /// <summary>Creates the coordinator that locks and unlocks the entries of <paramref name="menuViewModel"/>.</summary>
    /// <param name="menuViewModel">The menu view model, whose entries the coordinator locks.</param>
    /// <returns>The coordinator, or <see langword="null"/> when the router has none.</returns>
    IRouteableLockCoordinator? CreateLockCoordinator(IRouteableMenuViewModel menuViewModel);
}
