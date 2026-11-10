using Routeables.Abstraction.Guards;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Routeables.Abstraction.Errors;

/// <summary>
/// What a route change reports to <c>System.Diagnostics</c>, for dotnet-counters, dotnet-trace, OpenTelemetry or a listener of your
/// own. Nothing is recorded, and nothing costs more than a check, while nobody listens.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><b>Activity</b> <c>Routeables.Route</c> (source <see cref="Name"/>), one per route change, with the tags <c>routeables.kind</c>,
/// <c>routeables.route</c> (the template of the route, <c>/clients/{id:int}</c>), <c>routeables.view_model</c> and <c>routeables.outcome</c>.</description></item>
/// <item><description><b>Histogram</b> <c>routeables.route.duration</c> (ms), the time of a route change, with the tags <c>routeables.kind</c> and <c>routeables.outcome</c>.</description></item>
/// <item><description><b>Counter</b> <c>routeables.route.redirects</c>, the redirects a <see cref="IRouteGuard"/> made, with the tag <c>routeables.route</c> (the template of the route they lead to).</description></item>
/// </list>
/// The duration is the one of the router (guards, creation, initialization, lifecycle). The layout and drawing of the new view belong to the host.
/// </remarks>
public static class RouteableDiagnostics
{
    /// <summary>The name of the <see cref="ActivitySource"/> and of the <see cref="Meter"/>.</summary>
    public const string Name = "Routeables";

    /// <summary>The activity source: subscribe to it by this name.</summary>
    public static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>
    /// Adds the tag <c>routeables.address</c> (the address of the route, without its query) to the activity. Off by default: an address holds identifiers
    /// of the user's data. The metrics never carry it.
    /// </summary>
    public static bool IncludeAddress { get; set; }

    /// <summary>The meter: subscribe to it by this name.</summary>
    public static Meter Meter { get; } = new(Name);

    internal static readonly Histogram<double> RouteDuration =
        Meter.CreateHistogram<double>("routeables.route.duration", "ms", "The time a route change takes.");

    internal static readonly Histogram<double> GuardDuration =
        Meter.CreateHistogram<double>("routeables.guard.duration", "ms", "The time a route guard takes to decide, tagged with the guard (routeables.guard) and its decision (routeables.decision: allow, cancel, redirect).");

    internal static readonly Counter<long> Redirects =
        Meter.CreateCounter<long>("routeables.route.redirects", description: "The redirects made by route guards.");
}
