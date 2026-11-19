using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Routing;
using System.Diagnostics;

namespace Routeables.Routing;

/// <summary>
/// Provides ViewModel-first route change with history and lifecycle callbacks.
/// Routing operations are serialized per router instance.
/// </summary>
internal partial class Router(
    RouterHistory history,
    RouterGate gate,
    RouterLifecycle lifecycle,
    IServiceProvider provider) : IRouter, IRouteableLockCoordinatorFactory, IRouteGuardRefresh, IRouteTemplates
{
    /// <summary>
    /// Occurs when a routing operation is about to begin.
    /// </summary>
    public event EventHandler<RouteableRoutingEventArgs>? Routing;

    /// <summary>
    /// Occurs after a routing operation has completed (whether successful, failed, or cancelled).
    /// </summary>
    public event EventHandler<RouteableRoutedEventArgs>? Routed;

    /// <summary>
    /// Gets the route change history stack and current entry tracker.
    /// </summary>
    public IRouterHistory History => history;

    /// <summary>
    /// Gets a value indicating whether routing can go back to a previous entry.
    /// </summary>
    public bool CanGoBack => history.CanGoBack;

    /// <summary>
    /// Gets a value indicating whether routing can go forward to a subsequent entry.
    /// </summary>
    public bool CanGoForward => history.CanGoForward;

    /// <summary>
    /// Gets the view model instance of the currently active routing entry.
    /// </summary>
    public object? CurrentViewModel => history.Current?.ViewModel;

    /// <summary>
    /// Gets the view instance of the currently active routing entry.
    /// </summary>
    public object? CurrentView => history.Current?.View;


    // Observers must never break, or mask the error of, a navigation.
    private void Raise<T>(EventHandler<T>? handlers, object sender, T args) where T : EventArgs
        => RouteableEvents.Raise(provider, typeof(Router), handlers, sender, args);

    // Call only while the gate is held.
    private async Task<RouteableResult> ObserveAsync(
        RouteableKind kind, RouterEntry? from, RouterEntry to,
        CancellationToken ct, Func<Task<RouteableResult>> run, Redirection? redirection = null)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = RouteableDiagnostics.ActivitySource.StartActivity("Routeables.Route");
        if (activity is not null)
        {
            activity.SetTag("routeables.kind", kind.ToString());
            // The template (/clients/{id:int}) as http.route: a bounded set of values, with no identifier or token from the address.
            activity.SetTag("routeables.route", RouteTag(to.Args.Route));
            if (RouteableDiagnostics.IncludeAddress)
                activity.SetTag("routeables.address", PathOf(to.Args.Route));
            activity.SetTag("routeables.view_model", to.ViewModel.GetType().Name);
        }

        Raise(Routing, this, new RouteableRoutingEventArgs(kind, from, to));

        RouteableResult result;
        try
        {
            var decision = await AskGuardsAsync(kind, from, to, ct);
            if (decision.IsAllowed)
            {
                result = await run();
            }
            else
            {
                // A redirect runs after this navigation has ended: the gate is held until then.
                if (decision.RedirectTo is not null && redirection is not null)
                    redirection.Route = decision.RedirectTo;
                result = RouteableResult.Cancel();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Finish(activity, started, kind, RouteableOutcome.Cancelled);
            Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, RouteableOutcome.Cancelled));
            throw;
        }
        catch (Exception ex)
        {
            Finish(activity, started, kind, RouteableOutcome.Failed);
            Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, RouteableOutcome.Failed, ex));
            throw;
        }

        var outcome = result.Success ? RouteableOutcome.Success
                    : result.Cancelled ? RouteableOutcome.Cancelled
                    : RouteableOutcome.Failed;
        Finish(activity, started, kind, outcome);
        Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, outcome));
        return result;
    }

    private static void Finish(Activity? activity, long started, RouteableKind kind, RouteableOutcome outcome)
    {
        if (RouteableDiagnostics.RouteDuration.Enabled)
        {
            RouteableDiagnostics.RouteDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("routeables.kind", kind.ToString()),
                new KeyValuePair<string, object?>("routeables.outcome", outcome.ToString()));
        }

        if (activity is not null)
        {
            activity.SetTag("routeables.outcome", outcome.ToString());
            activity.SetStatus(outcome == RouteableOutcome.Failed ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        }
    }

    /// <inheritdoc />
    public IRouteableLockCoordinator? CreateLockCoordinator(IRouteableMenuViewModel menuViewModel)
    {
        // Locking is optional: without a registered factory (AddLocking) there is nothing to lock.
        return provider.GetService<Func<IRouteableMenuViewModel, IRouteableLockCoordinator>>()?.Invoke(menuViewModel);
    }

}