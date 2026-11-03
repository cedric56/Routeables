namespace Routeables.Abstraction;

/// <summary>
/// What a router offers to a menu view model: the coordinator that locks the menu while a page blocks leaving. Separate from
/// <see cref="IRouter"/>, which is about routing; <c>router.CreateLockCoordinator(menu)</c> asks it.
/// </summary>
internal interface ILockCoordinatorFactory
{
    /// <summary>Creates the coordinator that locks and unlocks the entries of <paramref name="mainViewModel"/>.</summary>
    /// <param name="mainViewModel">The main view model, whose entries the coordinator locks.</param>
    /// <returns>The coordinator, or <see langword="null"/> when the router has none.</returns>
    ILockCoordinator? CreateLockCoordinator(IMainViewModel mainViewModel);
}
