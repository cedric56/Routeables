using Routeables.Abstraction;

namespace Routeables.Routing;

internal static class RouteableEvents
{
    /// <summary>
    /// Raises an event for the observers of a route change. They must never break, or mask the error of, a route change: one that
    /// throws is reported (see <see cref="RouteableErrors"/>) and the others are still called.
    /// </summary>
    public static void Raise<T>(IServiceProvider provider, Type owner, EventHandler<T>? handlers, object sender, T args)
        where T : EventArgs
    {
        if (handlers is null) return;
        foreach (EventHandler<T> handler in handlers.GetInvocationList())
        {
            try { handler(sender, args); }
            catch (Exception ex) { RouteableErrors.Report(provider, ex, owner, typeof(T).Name); }
        }
    }
}
