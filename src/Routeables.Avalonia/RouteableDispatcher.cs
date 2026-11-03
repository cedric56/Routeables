using Avalonia.Threading;
using Routeables.Services;

namespace Routeables.Avalonia;

/// <summary>
/// The <see cref="IRouteableDispatcher"/> of Avalonia: <see cref="Dispatcher.UIThread"/>.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableDispatcher : RouteableDispatcherBase
{
    /// <inheritdoc />
    public override bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public override void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }
}
