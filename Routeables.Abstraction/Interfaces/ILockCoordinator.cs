namespace Routeables.Abstraction;

/// <summary>
/// Keeps the menu locked while a page refuses to be left: it listens to the lock messages of the broker and locks or unlocks every
/// entry of the main view model (see <see cref="IMainRouteables.Lock"/>). Created by <c>router.CreateLockCoordinator(menu)</c>.
/// </summary>
public interface ILockCoordinator
{
    /// <summary>Starts listening to the lock messages. Calling it again while subscribed does nothing.</summary>
    void Subscribe();

    /// <summary>Stops listening. The other coordinators that listen to the same broker are not affected.</summary>
    void Unsubscribe();
}
