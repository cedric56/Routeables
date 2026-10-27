namespace Routeables.Abstraction;

/// <summary>Creates the arguments of a route change. Replace how <see cref="IRouteableArgs"/> are built with <see cref="Extensions.RegisterRouteableArgsFactory"/>.</summary>
internal interface IRouteableArgsFactory
{
    /// <summary>A new, empty set of arguments.</summary>
    IRouteableArgs New();
}

/// <summary>
/// What travels with a route change: the values the view model is built with, the ones the view is built with, neutral values for the
/// lifecycle hooks, and the route. Build it with the <c>For…</c> and <c>With…</c> methods, which chain:
/// <c>RouteableArgs.Create().WithViewModel("id", 42).WithValue("animal", animal)</c>.
/// </summary>
public interface IRouteableArgs
{
    /// <summary>The values the view model is built with, matched to its constructor parameters (by type, in order, or by name).</summary>
    List<RouteableArgument> ViewModelParameters { get; }

    /// <summary>The values the view is built with, matched to its constructor parameters.</summary>
    List<RouteableArgument> ViewParameters { get; }

    /// <summary>Neutral values, available to the lifecycle hooks (<see cref="IRouteable"/>) but not injected anywhere.</summary>
    List<RouteableArgument> Values { get; }

    /// <summary>The error raised while a view model or a view of the route change was being built, which the router then reports; <see langword="null"/> otherwise.</summary>
    Exception? Exception { get; set; }

    /// <summary>The route the route change was made with (<c>/orders/42</c>), or <see langword="null"/> when it was not made by address.</summary>
    string? Route { get; set; }

    /// <summary>
    /// The router that is routing: set by the router itself, for the view model it builds. A view model whose constructor
    /// takes an <see cref="IRouter"/> receives this one, so a page shown in a region routes that region, not the default
    /// router. <see langword="null"/> outside a route change. Ignored when a router is already given with <c>ForViewModel</c>.
    /// </summary>
    IRouter? Router { get => null; set { } }

    /// <summary>
    /// The region the route change is meant for (the name given to <c>AddRegion</c>); <see langword="null"/> for the default router.
    /// Read by <c>provider.GoToAsync(url, args)</c>, which picks the router of that region. A router given directly
    /// (<c>router.GoToAsync</c>) is already one region's router and ignores it.
    /// </summary>
    string? Region { get => null; set { } }

    /// <summary>Sets the region of these arguments (see <see cref="Region"/>).</summary>
    /// <param name="region">The region name, or <see langword="null"/> for the default router.</param>
    /// <returns>These arguments, to chain.</returns>
    IRouteableArgs WithRegion(string? region) { Region = region; return this; }

    /// <summary>
    /// How the region brings in the view of this route change (<c>RouteTransition.Slide</c>); <see langword="null"/> leaves it to the
    /// region. Read by the hosts (<c>RouteableRegion</c>), which play the animation.
    /// </summary>
    RouteTransition? Transition { get => null; set { } }

    /// <summary>Sets the transition of these arguments (see <see cref="Transition"/>): <c>WithTransition(RouteTransition.Slide)</c> or <c>WithTransition("Slide")</c>.</summary>
    /// <param name="transition">The transition, or <see langword="null"/> to leave it to the region.</param>
    /// <returns>These arguments, to chain.</returns>
    IRouteableArgs WithTransition(RouteTransition? transition) { Transition = transition; return this; }

    /// <summary>Adds entries the view model is built with.</summary>
    /// <param name="entries">The entries, each with its value and optionally its name.</param>
    /// <returns>These arguments, to chain.</returns>
    IRouteableArgs ForViewModel(params RouteableArgument[] entries);

    /// <summary>Adds neutral entries (see <see cref="Values"/>).</summary>
    /// <param name="entries">The entries, each with its value and optionally its name.</param>
    /// <returns>These arguments, to chain.</returns>
    IRouteableArgs ForValues(params RouteableArgument[] entries);

    /// <summary>Adds entries the view is built with.</summary>
    /// <param name="entries">The entries, each with its value and optionally its name.</param>
    /// <returns>These arguments, to chain.</returns>
    IRouteableArgs ForView(params RouteableArgument[] entries);

    /// <summary>Sets the route of these arguments (see <see cref="Route"/>).</summary>
    /// <param name="route">The route, or <see langword="null"/> to clear it.</param>
    /// <returns>These arguments, to chain.</returns>
    IRouteableArgs WithRoute(string? route = null);

    // Shorthands: no RouteableArgument to build.

    /// <summary>Adds positional values for the view model (matched to constructor parameters by type): <c>ForViewModel(order, back)</c>.</summary>
    IRouteableArgs ForViewModel(params object?[] values) => ForViewModel(ToEntries(values));

    /// <summary>Adds positional values for the view.</summary>
    IRouteableArgs ForView(params object?[] values) => ForView(ToEntries(values));

    /// <summary>Adds neutral values (available to lifecycle hooks, not injected).</summary>
    IRouteableArgs ForValues(params object?[] values) => ForValues(ToEntries(values));

    /// <summary>Adds a named value for the view model: <c>WithViewModel("id", orderId)</c>.</summary>
    IRouteableArgs WithViewModel(string name, object? value, bool attribute = false) => ForViewModel(new RouteableArgument(value, name, attribute));

    /// <summary>Adds a named value for the view.</summary>
    IRouteableArgs WithView(string name, object? value, bool attribute = false) => ForView(new RouteableArgument(value, name, attribute));

    /// <summary>Adds a named neutral value: <c>WithValue("animal", animal)</c>.</summary>
    IRouteableArgs WithValue(string name, object? value, bool attribute = false) => ForValues(new RouteableArgument(value, name, attribute));

    // A RouteableArgument among the values is kept as it is.
    private static RouteableArgument[] ToEntries(object?[] values) =>
        values.Select(v => v as RouteableArgument ?? new RouteableArgument(v)).ToArray();

    /// <summary>The key a view is cached under (the entry named <c>Constants.Key</c> of <see cref="ViewParameters"/>), or <see langword="null"/>.</summary>
    public object? GetKeyForContent() =>
        ViewParameters.FirstOrDefault(v => v.name == Constants.Key)?.value;
}
