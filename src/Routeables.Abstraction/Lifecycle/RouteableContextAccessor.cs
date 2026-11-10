using Routeables.Abstraction.Bases;

namespace Routeables.Abstraction.Lifecycle;

/// <summary>An <see cref="IRouteableFor"/> over the data context of a view that is not one: what an <see cref="IRouteableViewBinder"/> returns.</summary>
/// <param name="get">Reads the data context of the view.</param>
/// <param name="set">Writes the data context of the view.</param>
public sealed class RouteableContextAccessor(Func<object?> get, Action<object?> set) : IRouteableFor
{
    /// <inheritdoc />
    public object? Context
    {
        get => get();
        set => set(value);
    }
}
