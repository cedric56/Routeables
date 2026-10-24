namespace Routeables.Abstraction;

/// <summary>
/// The routes registered with an explicit route, for example
/// <c>RegisterRoute&lt;CustomerView, CustomerViewModel&gt;(route: "/customers/{id:int}")</c> or <c>[Routeable("about")]</c>
/// (the leading <c>/</c> is optional), each with the call that routes a router to it. A host that has only a URL (Blazor address bar, deep link) uses it to find the route to
/// go to. Filled by the <c>RegisterRoute</c> overloads; one instance per service collection.
/// </summary>
internal sealed class RouteableTable
{
    private readonly List<(string Template, Func<IRouter, string, IRouteableArgs, CancellationToken, Task<RouteableResult>> Go)> _entries = [];
    private readonly object _gate = new();

    /// <summary>
    /// The registered routes in registration order. <c>Go(router, url, args, ct)</c> goes to <c>url</c>, which must match
    /// <c>Template</c> (see <c>RouteParser.TryMatch</c>).
    /// </summary>
    public IReadOnlyList<(string Template, Func<IRouter, string, IRouteableArgs, CancellationToken, Task<RouteableResult>> Go)> Entries
    {
        get { lock (_gate) return _entries.ToArray(); }
    }

    /// <summary>Takes the routes of another table (the one a module loaded while running filled); the ones already here are kept.</summary>
    internal void AddRange(RouteableTable other)
    {
        foreach (var (template, go) in other.Entries)
            Add(template, go);
    }

    internal void Add(string template, Func<IRouter, string, IRouteableArgs, CancellationToken, Task<RouteableResult>> go)
    {
        lock (_gate)
        {
            if (_entries.Any(e => string.Equals(e.Template, template, StringComparison.OrdinalIgnoreCase)))
                return;
            _entries.Add((template, go));
        }
    }
}