using Routeables.Abstraction.Routing;
using System.ComponentModel;

namespace Routeables.Abstraction.Guards;

/// <summary>Implemented by the router: runs the route guards again for the page that is shown.</summary>
public interface IRouteGuardRefresh
{
    /// <summary>
    /// Asks the guards whether the page shown may still be shown (the user signed out, a permission was withdrawn). When one redirects, the
    /// router goes there; when one refuses without a destination, nothing changes. Allowed or no page: <see cref="RouteableResult.Ok"/> and no change.
    /// Call it on the thread of the interface, like any route change.
    /// </summary>
    Task<RouteableResult> RefreshGuardsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Re-evaluates the guards when something changes.</summary>
public static class RouteGuardRefreshExtensions
{
    /// <summary>
    /// Runs the guards again for the page shown. The guards receive a change whose <see cref="RouteableRoutingEventArgs.From"/> and
    /// <see cref="RouteableRoutingEventArgs.To"/> are that page, with <see cref="RouteableKind.MoveTo"/>. A router that cannot do it returns <see cref="RouteableResult.Ok"/>.
    /// </summary>
    public static Task<RouteableResult> RefreshGuardsAsync(this IRouter router, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        return router is IRouteGuardRefresh refresh ? refresh.RefreshGuardsAsync(cancellationToken) : Task.FromResult(RouteableResult.Ok());
    }

    /// <summary>
    /// Re-evaluates the guards each time <paramref name="source"/> raises <c>PropertyChanged</c> for one of <paramref name="properties"/> (all of them when none is
    /// given): <c>router.RefreshGuardsOn(session, nameof(Session.IsSignedIn))</c>. Dispose the result to stop. Raise the event on the thread of the interface.
    /// A failure of the refresh is reported to <see cref="Errors.RouteableErrors"/> through <paramref name="provider"/> when one is given.
    /// </summary>
    public static IDisposable RefreshGuardsOn(this IRouter router, INotifyPropertyChanged source, IServiceProvider? provider = null, params string[] properties)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(source);
        async void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            // A null or empty name means "everything changed" (the INotifyPropertyChanged convention): it concerns every property.
            if (properties.Length > 0 && !string.IsNullOrEmpty(e.PropertyName) && !properties.Contains(e.PropertyName, StringComparer.Ordinal))
                return;
            try { await router.RefreshGuardsAsync(); }
            catch (Exception ex) { Errors.RouteableErrors.Report(provider, "RefreshGuardsOn", ex); }
        }

        source.PropertyChanged += OnChanged;
        return new Subscription(() => source.PropertyChanged -= OnChanged);
    }

    private sealed class Subscription(Action undo) : IDisposable
    {
        private Action? _undo = undo;
        public void Dispose() => Interlocked.Exchange(ref _undo, null)?.Invoke();
    }
}
