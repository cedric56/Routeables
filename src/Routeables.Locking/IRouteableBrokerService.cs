namespace Routeables.Locking;

/// <summary>A message of the <see cref="IRouteableBrokerService"/>: the topic says who receives it.</summary>
/// <param name="Topic">The topic subscribers listen to.</param>
public abstract record RouteableMessage(string Topic);

/// <summary>Published when the lock of the menu changes.</summary>
/// <param name="IsLocked">Whether a page refuses to be left.</param>
/// <param name="IsAllLocked">Whether every entry of the menu is locked.</param>
public record RouteableLockMessage(bool IsLocked, bool IsAllLocked) : RouteableMessage("LockChanged");

/// <summary>
/// A small publish/subscribe channel between components (the lock coordinator listens to it, a view model publishes on it).
/// It is safe to use from several threads.
/// </summary>
public interface IRouteableBrokerService
{
    /// <summary>
    /// Delivers <paramref name="message"/> to every subscriber of its topic. A subscriber that throws does not stop the others:
    /// the exceptions are thrown together once all of them were called.
    /// </summary>
    void Publish(RouteableMessage message);

    /// <summary>Subscribes <paramref name="callback"/> to <paramref name="topic"/>.</summary>
    /// <returns>A token: disposing it removes this subscription, and only this one.</returns>
    IDisposable Subscribe(string topic, Action<RouteableMessage> callback);
}

internal class RouteableBrokerService : IRouteableBrokerService
{
    // Copy-on-write: a publication reads an immutable snapshot and calls the subscribers outside the lock,
    // so a subscriber can subscribe, dispose or publish again without deadlocking.
    private readonly object _gate = new();
    private readonly Dictionary<string, Subscription[]> _subscriptions = [];

    public void Publish(RouteableMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Subscription[]? snapshot;
        lock (_gate)
            _subscriptions.TryGetValue(message.Topic, out snapshot);

        if (snapshot is null)
            return;

        List<Exception>? errors = null;
        foreach (var subscription in snapshot)
        {
            // One disposed while this message was on its way is not called.
            if (subscription.IsDisposed)
                continue;

            try
            {
                subscription.Callback(message);
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }

        if (errors is null)
            return;

        if (errors.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();

        throw new AggregateException(errors);
    }

    public IDisposable Subscribe(string topic, Action<RouteableMessage> callback)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(callback);

        var subscription = new Subscription(this, topic, callback);
        lock (_gate)
        {
            _subscriptions[topic] = _subscriptions.TryGetValue(topic, out var current)
                ? [.. current, subscription]
                : [subscription];
        }

        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscription.Topic, out var current))
                return;

            var remaining = current.Where(s => !ReferenceEquals(s, subscription)).ToArray();
            if (remaining.Length == 0)
                _subscriptions.Remove(subscription.Topic);
            else
                _subscriptions[subscription.Topic] = remaining;
        }
    }

    private sealed class Subscription(RouteableBrokerService owner, string topic, Action<RouteableMessage> callback) : IDisposable
    {
        private int _disposed;

        public string Topic { get; } = topic;

        public Action<RouteableMessage> Callback { get; } = callback;

        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(this);
        }
    }
}
