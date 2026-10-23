using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Segments;

/// <summary>
/// Maps segment names (<c>Orders</c> in <c>Orders?id=5/Detail</c>) to the view that a router goes to.
/// Filled by <see cref="SegmentExtensions.RegisterSegment{TViewModel}"/>.
/// </summary>
internal sealed class SegmentRegistry
{
    private readonly Dictionary<string, Func<IRouteableEntry>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public void Add(string name, Func<IRouteableEntry> factory)
    {
        lock (_gate)
        {
            if (!_factories.TryAdd(name, factory))
                throw new InvalidOperationException($"A segment named '{name}' is already registered.");
        }
    }

    public bool TryCreate(string name, out IRouteableEntry view)
    {
        Func<IRouteableEntry>? factory;
        lock (_gate)
            _factories.TryGetValue(name, out factory);

        view = factory?.Invoke()!;
        return factory is not null;
    }
}