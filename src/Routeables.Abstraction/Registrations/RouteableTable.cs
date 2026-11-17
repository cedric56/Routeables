namespace Routeables.Abstraction.Registrations;

/// <summary>
/// The routes registered with an explicit route, for example
/// <c>RegisterRoute&lt;CustomerView, CustomerViewModel&gt;(route: "/customers/{id:int}")</c> or <c>[Routeable("about")]</c>
/// (the leading <c>/</c> is optional), each with its view model. A host that has only a URL (Blazor address bar, deep link) and
/// <c>IRouter.GoToAsync(url)</c> use it to find the route to go to. Filled by the <c>RegisterRoute</c> overloads; one instance per service collection.
/// </summary>
internal sealed class RouteableTable
{
    // Copy on write: a route change reads the table several times, and the routes are added at start, so a read is the array itself and an add builds a new one.
    private volatile (string Template, Type? ViewModelType)[] _entries = [];
    private readonly object _gate = new();

    /// <summary>
    /// The registered routes in registration order: the template, and the view model it opens (<see langword="null"/> for a
    /// route without one: the view is found under the route itself).
    /// </summary>
    public IReadOnlyList<(string Template, Type? ViewModelType)> Entries
    {
        get => _entries;
    }

    /// <summary>Takes the routes of another table (the one a module loaded while running filled); the ones already here are kept.</summary>
    internal void AddRange(RouteableTable other)
    {
        foreach (var (template, viewModelType) in other.Entries)
            Add(template, viewModelType);
    }

    internal void Add(string template, Type? viewModelType)
    {
        lock (_gate)
        {
            if (_entries.Any(e => string.Equals(e.Template, template, StringComparison.OrdinalIgnoreCase)))
                return;
            _entries = [.. _entries, (template, viewModelType)];
        }
    }
}
