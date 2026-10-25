using Routeables.Services;

namespace Routeables.Wpf;

/// <summary>
/// The <see cref="IRouteableDispatcher"/> of WPF: the dispatcher of the application, whichever thread asks.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableDispatcher : RouteableDispatcherBase
{
    /// <inheritdoc />
    public override bool CheckAccess() => UiThread.Dispatcher.CheckAccess();

    /// <inheritdoc />
    public override void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        UiThread.Dispatcher.BeginInvoke(action);
    }
}
