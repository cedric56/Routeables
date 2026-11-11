using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Segments;

/// <summary>
/// Default <see cref="IRouteCoordinator"/>: resolves the router of each segment (the unkeyed <see cref="IRouter"/>, or the
/// keyed region), then runs the segments one after the other.
/// </summary>
internal sealed class RouteCoordinator(IServiceProvider provider, SegmentRegistry registry) : IRouteCoordinator
{
    public event EventHandler<RouteCoordinatingEventArgs>? Coordinating;

    public event EventHandler<RouteCoordinatedEventArgs>? Coordinated;

    private sealed record Step(RouteSegment Segment, IRouter Router, IRouteableMenuEntry? View);

    public async Task<RouteableResult> CoordinateAsync(RouteCoordination coordination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordination);

        // 1. Resolve everything first: a typo in the last segment must not leave the first ones applied.
        if (!TryResolve(coordination, out var steps, out var error))
        {
            ReportFailure(error);
            return RouteableResult.Fail(error);
        }

        Raise(Coordinating, new RouteCoordinatingEventArgs(coordination));

        var completed = 0;
        var result = RouteableResult.Ok();
        try
        {
            // 2. Absolute: start from empty histories, once per router. Erasing is not leaving: the page shown is asked first, as a route
            // change would (a page with unsaved changes can say no), and one "no" stops the whole coordination before anything is touched.
            var shownBefore = new Dictionary<IRouter, string?>();
            if (coordination.Absolute)
            {
                var routers = steps.Select(s => s.Router).Distinct().ToList();
                foreach (var router in routers)
                {
                    if (!await CanLeaveAsync(router, cancellationToken))
                    {
                        result = RouteableResult.Cancel();
                        Raise(Coordinated, new RouteCoordinatedEventArgs(coordination, result, 0));
                        return result;
                    }
                }

                foreach (var router in routers)
                {
                    shownBefore[router] = router.History.Current?.Args.Route;
                    await router.EraseHistoryAsync(cancellationToken);
                }
            }

            // 3. One coordination per segment, in order, stopping at the first that does not succeed.
            foreach (var step in steps)
            {
                result = await RunAsync(step, cancellationToken);
                if (!result.Success)
                    break;
                completed++;
            }

            // A segment that did not succeed after the histories were erased: a router that is still empty gets the page it showed back,
            // so that the application is not left with a blank region (what was applied before the failure stays).
            if (!result.Success)
                await BringBackAsync(shownBefore);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Raise(Coordinated, new RouteCoordinatedEventArgs(coordination, RouteableResult.Cancel(), completed));
            throw;
        }
        catch (Exception ex)
        {
            Raise(Coordinated, new RouteCoordinatedEventArgs(coordination, RouteableResult.Fail(ex), completed));
            throw;
        }

        // A failure is returned, not thrown: an application that ignores the result would never know, so it is reported too.
        // A cancellation (a guard that said no) is an answer, not an error.
        if (!result.Success && !result.Cancelled && result.Exception is { } failure)
            ReportFailure(failure);

        Raise(Coordinated, new RouteCoordinatedEventArgs(coordination, result, completed));
        return result;
    }

    // Whether the page a router shows agrees to be left (its CanLeaveAsync); a router with no page has nothing to ask.
    private static async Task<bool> CanLeaveAsync(IRouter router, CancellationToken cancellationToken)
    {
        if (router.History.Current is not { } current)
            return true;
        if (current.ViewModel is IRouteable viewModel && !await viewModel.CanLeaveAsync(cancellationToken))
            return false;
        return current.View is not IRouteable view || ReferenceEquals(view, current.ViewModel) || await view.CanLeaveAsync(cancellationToken);
    }

    private async Task BringBackAsync(Dictionary<IRouter, string?> shownBefore)
    {
        foreach (var (router, route) in shownBefore)
        {
            if (router.History.Count > 0 || string.IsNullOrWhiteSpace(route))
                continue;
            try { await router.GoToAsync(route, null, null, CancellationToken.None); }
            catch (Exception ex) { RouteableErrors.Report<RouteCoordinator>(provider, ex); }   // the failure of the segment is what the caller gets
        }
    }

    private void ReportFailure(Exception exception)
        => RouteableErrors.Report<RouteCoordinator>(provider, exception);

    private static Task<RouteableResult> RunAsync(Step step, CancellationToken cancellationToken)
        => step.Segment.Kind == RouteSegmentKind.Back
            ? step.Router.PopAsync(cancellationToken)
            : step.Router.GoToRouteAsync(step.View!, cancellationToken);

    private bool TryResolve(RouteCoordination coordination, out List<Step> steps, out Exception exception)
    {
        steps = new List<Step>(coordination.Segments.Count);
        exception = new Exception();

        foreach (var segment in coordination.Segments)
        {
            IRouter router;
            try
            {
                router = segment.Region is null
                    ? provider.GetRequiredService<IRouter>()
                    : provider.GetRouter(segment.Region);
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
                return false;
            }

            if (segment.Kind == RouteSegmentKind.Back)
            {
                steps.Add(new Step(segment, router, null));
                continue;
            }

            if (!registry.TryCreate(segment.Name, out var view))
            {
                exception = new Exception($"No segment named '{segment.Name}' is registered.");
                return false;
            }

            view.Arguments = BuildArguments(segment.Parameters, segment.Configure);
            steps.Add(new Step(segment, router, view));
        }
        return true;
    }

    // Named values: injected by property name on the view model (attribute entries), and kept in Values for IRouteable hooks.
    // Configure runs last, on the same RouteableArgs: it can add to, or complete, what the named parameters produced.
    private static IRouteableArgs BuildArguments(
        IReadOnlyList<KeyValuePair<string, object?>>? parameters, Action<IRouteableArgs>? configure)
    {
        var args = RouteableArgs.Create();
        AddNamed(args, parameters);
        configure?.Invoke(args);
        return args;
    }

    private static void AddNamed(IRouteableArgs args, IReadOnlyList<KeyValuePair<string, object?>>? parameters)
    {
        if (parameters is null) return;

        foreach (var group in parameters.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var values = group.Select(p => p.Value).ToArray();
            object? value = values.Length == 1 ? values[0]
                : values.All(v => v is null or string) ? values.Cast<string?>().ToArray()
                : values;

            args.ForViewModel(new RouteableArgument(value, group.Key, true));
            args.ForValues(new RouteableArgument(value, group.Key, true));
        }
    }

    // Observers must never break, or mask the error of, a coordination (same rule as Router).
    private void Raise<T>(EventHandler<T>? handlers, T args) where T : EventArgs
        => RouteableEvents.Raise(provider, typeof(RouteCoordinator), handlers, this, args);
}