using Routeables.Abstraction.Routing;
using System.Collections.ObjectModel;
namespace Routeables.Abstraction.Registrations;

/// <summary>
/// The routes registered with an explicit route, for example
/// <c>RegisterRoute&lt;CustomerView, CustomerViewModel&gt;(route: "/customers/{id:int}")</c> or <c>[Routeable("about")]</c>
/// (the leading <c>/</c> is optional), each with its view model. A host that has only a URL (Blazor address bar, deep link) and
/// <c>IRouter.GoToAsync(url)</c> use it to find the route to go to. Filled by the <c>RegisterRoute</c> overloads; one instance per service collection.
/// </summary>
internal sealed class RouteableTable
{
    // Copy on write. Navigations read the table often and routes are added at startup, so a read returns the collection itself (no copy, and
    // nothing a caller could cast back to an array and change: the table is shared by the whole application), and an add builds a new one.
    private volatile ReadOnlyCollection<(string Template, Type? ViewModelType)> _entries = new([]);
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

    /// <summary>Forgets every route (for the tests: a page that the menu lists and the table does not).</summary>
    internal void Clear()
    {
        lock (_gate)
            _entries = new([]);
    }

    internal void Add(string template, Type? viewModelType)
    {
        lock (_gate)
        {
            // The same template, query included: "/search" and "/search?mode=grid" are two routes (the parser tells them apart), not one registered twice.
            var at = -1;
            for (var i = 0; i < _entries.Count && at < 0; i++)
                if (RouteAddress.Same(_entries[i].Template, template))
                    at = i;

            if (at >= 0)
            {
                // The container keeps the last view registered under a route (a module replaces a page's view), and the table follows it, so that the view
                // model read here is that view's. A registration without a view model, or on a route that has none, changes nothing.
                if (viewModelType is not null && _entries[at].ViewModelType is { } known && known != viewModelType)
                {
                    var replaced = _entries.ToArray();
                    replaced[at] = (_entries[at].Template, viewModelType);
                    _entries = new(replaced);
                }

                return;
            }

            _entries = new([.. _entries, (template, viewModelType)]);
        }
    }
}
