using Routeables.Services;

namespace Routeables.Maui;

/// <summary>
/// The <see cref="IRouteableDispatcher"/> of MAUI: the dispatcher of the application. Before the application exists there is no thread
/// of the interface to go to, and the work runs on the calling thread.
/// </summary>
internal sealed class RouteableDispatcher : RouteableDispatcherBase
{
    public override bool CheckAccess() => Application.Current?.Dispatcher is not { } dispatcher || !dispatcher.IsDispatchRequired;

    public override void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.Dispatch(action);
        else
            action();
    }
}
