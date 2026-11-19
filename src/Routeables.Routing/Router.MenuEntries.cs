using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;

namespace Routeables.Routing;

// Opening a page from an entry of a menu, including entries typed by a view model.
internal partial class Router
{
    private Task<RouteableResult> GoToEntryAsync(IRouteableMenuEntry routeableView, CancellationToken cancellationToken, string[] viewKeys, IRouteableArgs? supplied = null, string? address = null)
    {
        Respell(routeableView);
        // A typed entry (RouteableMenuEntry<TViewModel>) names the keys of its view model; any other entry is bound by its template alone.
        var type = GetRouteableEntryType(routeableView);
        if (type is null)
            return GoToViewAsync(routeableView, routeableView.Template, routeableView.Template, cancellationToken, viewKeys, supplied, null, address);

        ResolveTypedEntry(routeableView, type);
        Respell(routeableView);
        var keys = RouteableKeys.GetKeys(type, routeableView.Template);
        return GoToViewAsync(routeableView, keys.viewKey, keys.viewModelKey, cancellationToken, viewKeys, supplied, type, address);
    }

    // The view model a typed menu entry (RouteableMenuEntry<T>) is for; any other entry is bound by its template alone.
    static Type? GetRouteableEntryType(IRouteableMenuEntry view) => (view as RouteableMenuEntry)?.ViewModelType;

    private Task<RouteableResult> GoToEntryAsync(IRouteableMenuEntry routeableView, Type viewModelType, CancellationToken cancellationToken, string[] viewKeys)
    {
        ResolveTypedEntry(routeableView, viewModelType);
        Respell(routeableView);
        var keys = RouteableKeys.GetKeys(viewModelType, routeableView.Template);
        return GoToViewAsync(routeableView, keys.viewKey, keys.viewModelKey, cancellationToken, viewKeys, null, viewModelType);
    }

    // "about" and "/about" are one template, and a page is registered under the spelling it was registered with. An entry that spells it the
    // other way gets that spelling, or its keys would name a view that does not exist.
    private void Respell(IRouteableMenuEntry entry)
    {
        if (entry is not RouteableMenuEntry typed || string.IsNullOrEmpty(typed.Template))
            return;

        var registered = provider.GetService<RouteableTable>()?.Entries
            .Select(e => e.Template)
            .FirstOrDefault(t => !string.Equals(t, typed.Template, StringComparison.Ordinal)
                && string.Equals(RouteAddress.Absolute(t), RouteAddress.Absolute(typed.Template), StringComparison.OrdinalIgnoreCase));
        if (registered is not null && !provider.GetService<RouteableTable>()!.Entries.Any(e => string.Equals(e.Template, typed.Template, StringComparison.Ordinal)))
            typed.Template = registered;
    }

    // A typed entry (RouteableMenuEntry<T>) created without a template or route holds the internal key of its type for whatever it lacks.
    // The view model's registered route is read here, because the entry cannot access the table: the template of the address it was given,
    // or else the view model's own route. The page's keys come from its template, as everywhere else.
    private void ResolveTypedEntry(IRouteableMenuEntry entry, Type type)
    {
        if (entry is not RouteableMenuEntry typed)
            return;

        var internalKey = RouteableKeys.GetRoute(type);
        var noTemplate = string.Equals(typed.Template, internalKey, StringComparison.Ordinal);
        var noRoute = string.Equals(typed.Route, internalKey, StringComparison.Ordinal);
        if (noTemplate && noRoute)
        {
            var registered = provider.GetService<RouteableTable>()?.Entries
                .Where(e => e.ViewModelType == type).Select(e => e.Template).FirstOrDefault();
            if (registered is not null)
            {
                typed.Template = registered;
                typed.Route = registered;
            }
        }
        else if (noTemplate)
        {
            if (TryTemplateOf(typed.Route, out var template))
                typed.Template = template;
        }
        else if (noRoute && !typed.Template.Contains('{'))
        {
            typed.Route = typed.Template;
        }
    }
}
