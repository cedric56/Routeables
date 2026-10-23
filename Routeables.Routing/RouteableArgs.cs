using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Routing arguments: explicit ViewModel vs View bags, plus a neutral Values bag
/// (route/host). Acts as a fluent builder — each With*/For* method modifies the instance and returns it for chaining.
/// </summary>
public class RouteableArgs : IRouteableArgs, IRouteableArgsFactory
{
    /// <summary>
    /// Gets or sets any exception that occurred during route change or argument parsing.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets the target route identifier.
    /// </summary>
    public string? Route { get; set; }

    /// <summary>The router that is routing (see <see cref="IRouteableArgs.Router"/>).</summary>
    public IRouter? Router { get; set; }

    /// <summary>
    /// Gets or sets the parameters specifically meant for the ViewModel.
    /// </summary>
    public List<RouteableArgument> ViewModelParameters { get; init; } = new();

    /// <summary>
    /// Gets or sets the parameters specifically meant for the View.
    /// </summary>
    public List<RouteableArgument> ViewParameters { get; init; } = new();

    /// <summary>
    /// Gets or sets neutral values (such as route or host parameters).
    /// </summary>
    public List<RouteableArgument> Values { get; init; } = new();

    /// <summary>
    /// Creates a new, empty instance of <see cref="RouteableArgs"/>.
    /// </summary>
    /// <returns>A new <see cref="IRouteableArgs"/> instance.</returns>
    public static IRouteableArgs Create() => new RouteableArgs();

    // The factory interface is internal: Create() is the public way to get an empty instance.
    IRouteableArgs IRouteableArgsFactory.New() => new RouteableArgs();

    /// <summary>
    /// Sets or updates the target route.
    /// </summary>
    /// <param name="route">The route string to assign.</param>
    /// <returns>The current <see cref="IRouteableArgs"/> instance for method chaining.</returns>
    public IRouteableArgs WithRoute(string? route = null)
    {
        Route = route;
        return this;
    }

    /// <summary>
    /// Adds entries to the ViewModel parameters bag.
    /// </summary>
    /// <param name="entries">The entries to add.</param>
    /// <returns>The current <see cref="IRouteableArgs"/> instance for method chaining.</returns>
    public IRouteableArgs ForViewModel(params RouteableArgument[] entries)
        => AddRange(ViewModelParameters, entries);

    /// <summary>
    /// Adds entries to the neutral values bag.
    /// </summary>
    /// <param name="entries">The entries to add.</param>
    /// <returns>The current <see cref="IRouteableArgs"/> instance for method chaining.</returns>
    public IRouteableArgs ForValues(params RouteableArgument[] entries)
        => AddRange(Values, entries);

    /// <summary>
    /// Adds entries to the View parameters bag.
    /// </summary>
    /// <param name="entries">The entries to add.</param>
    /// <returns>The current <see cref="IRouteableArgs"/> instance for method chaining.</returns>
    public IRouteableArgs ForView(params RouteableArgument[] entries)
        => AddRange(ViewParameters, entries);

    /// <inheritdoc cref="IRouteableArgs.ForViewModel(object?[])"/>
    public IRouteableArgs ForViewModel(params object?[] values) => ForViewModel(ToEntries(values));

    /// <inheritdoc cref="IRouteableArgs.ForView(object?[])"/>
    public IRouteableArgs ForView(params object?[] values) => ForView(ToEntries(values));

    /// <inheritdoc cref="IRouteableArgs.ForValues(object?[])"/>
    public IRouteableArgs ForValues(params object?[] values) => ForValues(ToEntries(values));

    /// <inheritdoc cref="IRouteableArgs.WithViewModel(string, object?, bool)"/>
    public IRouteableArgs WithViewModel(string name, object? value, bool attribute = false) => ForViewModel(new RouteableArgument(value, name, attribute));

    /// <inheritdoc cref="IRouteableArgs.WithView(string, object?, bool)"/>
    public IRouteableArgs WithView(string name, object? value, bool attribute = false) => ForView(new RouteableArgument(value, name, attribute));

    /// <inheritdoc cref="IRouteableArgs.WithValue(string, object?, bool)"/>
    public IRouteableArgs WithValue(string name, object? value, bool attribute = false) => ForValues(new RouteableArgument(value, name, attribute));

    // A RouteableArgument among the values is kept as it is.
    private static RouteableArgument[] ToEntries(object?[] values) =>
        values.Select(v => v as RouteableArgument ?? new RouteableArgument(v)).ToArray();

    /// <summary>
    /// Helper method to append a collection of entries to a target list.
    /// </summary>
    private IRouteableArgs AddRange(List<RouteableArgument> targetList, params RouteableArgument[] values)
    {
        targetList.AddRange(values);
        return this;
    }
}