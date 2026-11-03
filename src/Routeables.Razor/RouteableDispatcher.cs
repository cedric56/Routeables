using Routeables.Services;

namespace Routeables.Razor;

/// <summary>
/// The <see cref="IRouteableDispatcher"/> of Blazor: the synchronization context the service was created in. In Blazor Server that is the
/// one of the circuit, where a component may be changed; in WebAssembly there is one thread, and everything runs on it.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableDispatcher : RouteableDispatcherBase
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <inheritdoc />
    public override bool CheckAccess() => _context is null || SynchronizationContext.Current == _context;

    /// <inheritdoc />
    public override void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_context is null)
            action();
        else
            _context.Post(static state => ((Action)state!)(), action);
    }
}
