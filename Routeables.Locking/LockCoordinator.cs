using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Locking;

/// <summary>
/// Coordinates lock states across routeable components based on incoming broker messages.
/// It listens with its own subscription: disposing it (or calling <see cref="Unsubscribe"/>) stops this coordinator only,
/// not the others that listen to the same broker.
/// </summary>
internal sealed class LockCoordinator : ILockCoordinator, IDisposable
{
    private readonly object _gate = new();
    private readonly IServiceProvider _provider;
    private readonly IRouteableBrokerService _broker;
    private readonly IMainViewModel _routeables;
    private IDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockCoordinator"/> class.
    /// </summary>
    /// <param name="provider">
    /// The service provider used to resolve the other <see cref="IMainViewModel"/> instances. It is only used when a message
    /// is addressed to all of them, never while the coordinator is created: resolving them here would build the main
    /// view model that is creating this coordinator again.
    /// </param>
    /// <param name="broker">The broker service used for subscribing to lock change messages.</param>
    /// <param name="manager">The primary routeable manager instance.</param>
    public LockCoordinator(IServiceProvider provider, IRouteableBrokerService broker, IMainViewModel manager)
    {
        _provider = provider;
        _broker = broker;
        _routeables = manager;
    }

    /// <summary>
    /// Subscribes to the "LockChanged" message topic to handle lock or unlock updates dynamically.
    /// Calling it again while subscribed does nothing.
    /// </summary>
    public void Subscribe()
    {
        lock (_gate)
            _subscription ??= _broker.Subscribe("LockChanged", OnMessage);
    }

    private void OnMessage(RouteableMessage message)
    {
        if (message is not RouteableLockMessage m)
            return;

        Apply(_routeables, m.IsLocked);

        if (!m.IsAllLocked)
            return;

        IEnumerable<IMainViewModel> all;
        try
        {
            all = _provider.GetServices<IMainViewModel>().ToList();
        }
        catch (ObjectDisposedException)
        {
            // The scope this coordinator belongs to is gone: there is no one left to lock.
            return;
        }

        foreach (var manager in all)
        {
            if (!ReferenceEquals(manager, _routeables))
                Apply(manager, m.IsLocked);
        }
    }

    private static void Apply(IMainViewModel manager, bool isLocked)
    {
        if (isLocked)
            manager.Lock();
        else
            manager.Unlock();
    }

    /// <summary>
    /// Performs tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() => Unsubscribe();

    /// <summary>
    /// Stops listening to the "LockChanged" message topic.
    /// </summary>
    public void Unsubscribe()
    {
        IDisposable? subscription;
        lock (_gate)
        {
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
    }
}
