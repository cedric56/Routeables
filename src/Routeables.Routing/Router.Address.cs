using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;

namespace Routeables.Routing;

// Opening a page from an address: the qualifiers of the history (-, -/route), the template that serves it and its tag for diagnostics.
internal partial class Router
{
    // "-/orders" left a page for a route that did not open: do not leave the user on the page before it. Only do this when nothing else moved
    // the history meanwhile (the page that was left is still the next one), and without the caller's token, which may be why the route did not open.
    private async Task ShowLeftAgainAsync(object? left)
    {
        if (ReferenceEquals(history.PeekForward(), left))
            await ForwardAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<RouteableResult> GoToAsync(
        string url, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        // Qualifiers: "-" goes back one page; "-/orders" goes back, then to /orders (the new page replaces the one being left). If /orders does
        // not open, the page that was left is shown again.
        // Checked first because they are not routes, so a back button (RouteableLink Route="-") needs no code.
        var trimmed = url.Trim();
        if (trimmed == "-")
            return await GoBackAsync(cancellationToken);
        if (trimmed.StartsWith("-/", StringComparison.Ordinal) && trimmed.Length > 2)
        {
            var left = history.Current;
            var back = await GoBackAsync(cancellationToken);
            if (!back.Success)
                return back;

            RouteableResult forward;
            try { forward = await GoToAsync(trimmed[1..], args, template, cancellationToken); }
            catch
            {
                // An exception or a cancellation also means the route did not open: show the page that was left again, then rethrow.
                try { await ShowLeftAgainAsync(left); }
                catch (Exception) { /* the exception of the route is the one that matters */ }
                throw;
            }

            if (!forward.Success)
                await ShowLeftAgainAsync(left);

            return forward;
        }

        url = trimmed; // the address the qualifier was read from, without the spaces around it

        // A route registered with an explicit template (RegisterRoute(route: "/orders/{id:int}"), [Routeable]): the template reads the parameters
        // from the address. When a template is given, the route is the registered one with that template.
        var entries = provider.GetService<RouteableTable>()?.Entries ?? [];
        var found = template is null
            ? RouteParser.BestMatch(entries, url) // several templates may match: take the most specific
            : entries.FirstOrDefault(e => string.Equals(RouteAddress.Absolute(e.Template), RouteAddress.Absolute(template), StringComparison.OrdinalIgnoreCase)); // "about" and "/about" are the same template, as in the table
        if (found.Template is not null)
        {
            var keys = found.ViewModelType is null
                ? (viewKey: found.Template, viewModelKey: found.Template)
                : RouteableKeys.GetKeys(found.ViewModelType, found.Template);
            // RouteParser.TryMatch fills the instance it is given; without one, the parsed route values would be lost.
            return await GoToRouteAsync(url, found.Template, keys.viewKey, keys.viewModelKey, args ?? RouteableArgs.Create(), null, null, cancellationToken);
        }

        // An unregistered template is the route itself: the view and the view model are found under it.
        if (template is not null)
            return await GoToRouteAsync(url, template, template, template, args, null, null, cancellationToken);

        if (provider.GetService<IRouteableMenuViewModel>()?.Entries.FirstOrDefault(r => RouteAddress.SamePath(r.Route, url)) is { } item)
        {
            // The entry has no template to read the address with, but nothing the caller gave is lost: its args and the address's query are kept.
            var query = RouteParser.QueryOf(url);
            if (args is null && query.Count == 0)
                return await GoToEntryAsync(item, cancellationToken, []);

            var supplied = Snapshot(item.Arguments);
            if (args is not null)
            {
                // By name, like the values: a caller's parameter replaces the entry's parameter of the same name (the constructor takes the first value
                // of a name and the property the last, so both must see the same one).
                foreach (var parameter in args.ViewModelParameters)
                    RouteParser.SetValue(supplied.ViewModelParameters, parameter);
                foreach (var parameter in args.ViewParameters)
                    RouteParser.SetValue(supplied.ViewParameters, parameter);
                foreach (var value in args.Values)
                    RouteParser.SetValue(supplied.Values, value); // a caller's value replaces the entry's value of the same name
            }
            // The requested address is the route of this navigation (history, browser URL), query included; matching it adds the query values.
            return await GoToEntryAsync(item, cancellationToken, [], supplied, url);
        }

        // Nothing registered at all is a common first mistake: say where to look.
        var hint = entries.Any()
            ? string.Empty
            : " No route is registered at all: check that the projects with the [Routeable] view models and the views reference the Routeables package (it carries the source generator). An executable registers its generated routes by itself; a library, or a project built with RouteablesAutoRegister=false, needs its AddGeneratedRoutes() called.";
        // Show the not-found page (UseNotFoundRoute) for addresses nobody serves, unless the missing address is that page itself.
        if (provider.GetService<RouteableNotFound>() is { } notFound && !RouteAddress.SamePath(notFound.Route, url))
        {
            // Work on a copy: the caller's args must not collect a notFoundUrl for every missing address.
            var shown = Snapshot(args);
            RouteParser.SetValue(shown.Values, new RouteableArgument(url, RouteableNotFound.UrlValue));
            return await GoToAsync(notFound.Route, shown, null, cancellationToken);
        }

        // A placeholder that the parser reads as fixed text is the likely reason a registered route does not match.
        if (entries.Select(e => (e.Template, Piece: RouteParser.LooksLikeAPlaceholder(e.Template))).FirstOrDefault(e => e.Piece is not null) is { Piece: { } piece } suspect)
            hint += $" The registered template '{suspect.Template}' has '{piece}', which is read as fixed text: a placeholder is written {{name}}, {{name:type}} or {{name:type?}} (optional), and in a query {{name}} on its own, not name={{name}}.";

        return RouteableResult.Fail(new InvalidOperationException($"No route matches '{url}'. Register it with an explicit route, for instance RegisterRoute<TView, TViewModel>(route: \"/orders/{{id:int}}\")." + hint));
    }

    // How a route is named in telemetry: by its template, never by its address (addresses hold identifiers, and one series per address is
    // unbounded). A route that no template names (a direct navigation, a menu entry without a template) is "(unmatched)": the address itself would put identifiers in the tag.
    private string RouteTag(string? route)
        => string.IsNullOrWhiteSpace(route) ? string.Empty : TryTemplateOf(route, out var template) ? PathOf(template) : UnmatchedRouteTag;

    internal const string UnmatchedRouteTag = "(unmatched)";

    // The address without its query and fragment, either of which can hold a token ("#access_token=" in an implicit OAuth flow).
    internal static string PathOf(string? route)
    {
        var address = route ?? string.Empty;
        var end = address.AsSpan().IndexOfAny('?', '#');
        return end < 0 ? address : address[..end];
    }

    // The template registered for a route: the route itself, one that matches it, the one of a menu entry, or else the route.
    private string TemplateOf(string route) => TryTemplateOf(route, out var template) ? template : route;

    string? IRouteTemplates.TemplateOf(string route) => TryTemplateOf(route, out var template) ? template : null;

    private bool TryTemplateOf(string route, out string template)
    {
        var entries = provider.GetService<RouteableTable>()?.Entries ?? [];
        // The template that is the route itself (query included: "/search" and "/search?mode=grid" are two routes), then one that matches it,
        // then one with the same path.
        var exact = entries.FirstOrDefault(e => RouteAddress.Same(e.Template, route));
        if (exact.Template is not null)
            return Found(exact.Template, out template);

        var matching = RouteParser.BestMatch(entries, route);
        if (matching.Template is not null)
            return Found(matching.Template, out template);

        var samePath = entries.FirstOrDefault(e => RouteAddress.SamePath(e.Template, route));
        if (samePath.Template is not null)
            return Found(samePath.Template, out template);

        var item = provider.GetService<IRouteableMenuViewModel>()?.Entries
            .FirstOrDefault(r => RouteAddress.SamePath(r.Route, route) && !string.IsNullOrEmpty(r.Template));
        if (item?.Template is { } menuTemplate)
            return Found(menuTemplate, out template);

        template = route;
        return false;

        static bool Found(string value, out string template) { template = value; return true; }
    }
}
