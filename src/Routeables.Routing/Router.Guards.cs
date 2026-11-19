using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using System.Diagnostics;

namespace Routeables.Routing;

// Route guards, type guards and the redirections they ask for.
internal partial class Router
{
    // The route a guard sent the user to instead; read once the gate is released.
    private sealed class Redirection
    {
        public string? Route { get; set; }
    }

    // How many redirects follow one another in the flow of one call: a guard that sends the user back to a page it refuses would loop for ever.
    private const int MaxRedirects = 8;
    private static readonly AsyncLocal<int> Redirects = new();

    // The guards are resolved once, when the first route change needs them, and kept sorted by IRouteGuard.Order (a stable sort: guards
    // of the same order stay in the order of registration).
    private IRouteGuard[]? _guards;

    private IRouteGuard[] Guards => _guards ??= [.. provider.GetServices<IRouteGuard>().Select((guard, index) => (guard, index))
        .OrderBy(x => x.guard.Order).ThenBy(x => x.index).Select(x => x.guard)];

    // Every registered guard, in the order of IRouteGuard.Order, until one refuses.
    private async Task<RouteGuardDecision> AskGuardsAsync(RouteableKind kind, RouterEntry? from, RouterEntry to, CancellationToken ct)
    {
        var guards = Guards;
        if (guards.Length == 0)
            return RouteGuardDecision.Allow;

        var change = new RouteableRoutingEventArgs(kind, from, to);
        var measure = RouteableDiagnostics.GuardDuration.Enabled;
        foreach (var guard in guards)
        {
            var started = measure ? Stopwatch.GetTimestamp() : 0;
            // A guard that answers nothing is a bug of the guard: it is not taken for a yes (a guard that fails opens the door), the route change fails and says which one.
            var decision = await guard.CanEnterAsync(change, ct)
                ?? throw new InvalidOperationException($"The route guard {guard.GetType().FullName} returned null: it must return RouteGuardDecision.Allow, Cancel or Redirect.");
            if (measure)
                RouteableDiagnostics.GuardDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    new KeyValuePair<string, object?>("routeables.guard", guard.GetType().Name),
                    new KeyValuePair<string, object?>("routeables.decision", decision.IsAllowed ? "allow" : decision.RedirectTo is null ? "cancel" : "redirect"));
            if (!decision.IsAllowed)
                return decision;
        }

        return RouteGuardDecision.Allow;
    }

    /// <inheritdoc />
    public async Task<RouteableResult> RefreshGuardsAsync(CancellationToken cancellationToken = default)
    {
        // Like every route change, one at a time: the page asked about is the one that is shown when the guards answer, and a refresh
        // that comes in the middle of a route change waits for it.
        // The token of the caller goes on to the redirection: the one the gate gives is linked to a source that is released when the gate is.
        var (asked, decision) = await RunGatedAsync(cancellationToken, async token =>
        {
            var current = history.Current;
            return current is null
                ? (current, RouteGuardDecision.Allow)
                : (current, await AskGuardsAsync(RouteableKind.MoveTo, current, current, token));
        });
        if (asked is null)
            return RouteableResult.Ok();

        if (decision.IsAllowed)
            return RouteableResult.Ok();
        // Nowhere to send the user: the page stays, as for any guard that cancels. A page that is no longer the one shown (a route change came
        // between the answer and now) is not redirected: the answer was about another page.
        if (decision.RedirectTo is not { } route || !ReferenceEquals(history.Current, asked))
            return RouteableResult.Cancel();
        return await RedirectAsync(route, cancellationToken);
    }

    // Goes where a guard sent the user, once the route change that was refused has ended and released the gate.
    private async Task<RouteableResult> RedirectAsync(string route, CancellationToken ct)
    {
        if (Redirects.Value >= MaxRedirects)
            return RouteableResult.Fail(new InvalidOperationException(
                $"More than {MaxRedirects} redirects in a row, the last one to \"{route}\": a route guard sends the user to a route that another guard (or the same one) refuses."));

        if (RouteableDiagnostics.Redirects.Enabled)
            RouteableDiagnostics.Redirects.Add(1, new KeyValuePair<string, object?>("routeables.route", RouteTag(route)));

        Redirects.Value++;
        try
        {
            return await GoToAsync(route, null, null, ct);
        }
        finally
        {
            Redirects.Value--;
        }
    }

    private IRouteTypeGuard[]? _typeGuards;

    private IRouteTypeGuard[] TypeGuards => _typeGuards ??= [.. provider.GetServices<IRouteTypeGuard>().Select((guard, index) => (guard, index))
        .OrderBy(pair => pair.guard.Order).ThenBy(pair => pair.index).Select(pair => pair.guard)];

    // The result of a route change that the type guards refuse (null when they allow it): cancelled, or the route change of the redirection.
    private async Task<RouteableResult?> AskTypeGuardsAsync(string template, string route, Type? requestedType, CancellationToken cancellationToken)
    {
        var guards = TypeGuards;
        if (guards.Length == 0)
            return null;

        // The type of the route table; a page registered with no route (opened from a menu) is not in it, and is the type the request names.
        // The template itself first (query included), then the one with the same path.
        var table = provider.GetService<RouteableTable>()?.Entries ?? [];
        var known = table.Where(entry => RouteAddress.Same(entry.Template, template)).Concat(table.Where(entry => RouteAddress.SamePath(entry.Template, template))).FirstOrDefault();
        var type = known.ViewModelType ?? requestedType;
        if (type is null)
            return null;

        foreach (var guard in guards)
        {
            var decision = await guard.CanBuildAsync(type, route, cancellationToken)
                ?? throw new InvalidOperationException($"The route guard {guard.GetType().FullName} returned null: it must return RouteGuardDecision.Allow, Cancel or Redirect.");
            if (decision.IsAllowed)
                continue;

            return decision.RedirectTo is { } to ? await RedirectAsync(to, cancellationToken) : RouteableResult.Cancel();
        }

        return null;
    }
}
