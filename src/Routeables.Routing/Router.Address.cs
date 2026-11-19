using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using System.Diagnostics;

namespace Routeables.Routing;

// Opening a page from an address: the qualifiers of the history (-, -/route), the template that serves it and its tag for diagnostics.
internal partial class Router
{
    // "-/orders" left a page for a route that did not open: the user is not left on the page before. Only when nothing else moved the history in the
    // meantime (the page left is still the next one), and without the caller's token, which may be the reason the route did not open.
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

        // Qualifiers: "-" goes back one page, "-/orders" goes back and then to /orders (a page replaces the one being left in the user's mind);
        // when /orders does not open, the page that was left is shown again.
        // Checked first: they are not routes, and a back button (RouteableLink Route="-") needs no code.
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
                // An exception (or a cancellation) is a route that did not open too: the page that was left is shown again, then it goes on.
                try { await ShowLeftAgainAsync(left); }
                catch (Exception) { /* the exception of the route is the one that matters */ }
                throw;
            }

            if (!forward.Success)
                await ShowLeftAgainAsync(left);

            return forward;
        }

        url = trimmed;      // the address the qualifier was read from: the spaces around it are not part of the route

        // A route registered with an explicit template (RegisterRoute(route: "/orders/{id:int}"), [Routeable]): the template
        // reads the parameters from the address. Given a template, it is the registered route with that template.
        var entries = provider.GetService<RouteableTable>()?.Entries ?? [];
        var found = template is null
            ? RouteParser.BestMatch(entries, url)   // several templates may match: the most specific one
            : entries.FirstOrDefault(e => string.Equals(RouteAddress.Absolute(e.Template), RouteAddress.Absolute(template), StringComparison.OrdinalIgnoreCase));   // "about" and "/about" are one template, as for the table
        if (found.Template is not null)
        {
            var keys = found.ViewModelType is null
                ? (viewKey: found.Template, viewModelKey: found.Template)
                : RouteableKeys.GetKeys(found.ViewModelType, found.Template);
            // RouteParser.TryMatch fills the instance it is given; without one the parsed route values would be lost.
            return await GoToRouteAsync(url, found.Template, keys.viewKey, keys.viewModelKey, args ?? RouteableArgs.Create(), null, null, cancellationToken);
        }

        // A template that is not registered is the route itself: the view and the view model are found under it.
        if (template is not null)
            return await GoToRouteAsync(url, template, template, template, args, null, null, cancellationToken);

        // A page of the menu, whose route has no parameter.
        if (provider.GetService<IRouteableMenuViewModel>()?.Entries.FirstOrDefault(r => RouteAddress.SamePath(r.Route, url)) is { } item)
        {
            // The entry has no template to read the address with, but what the caller gave is not lost: its args, and the query of the address.
            var query = RouteParser.QueryOf(url);
            if (args is null && query.Count == 0)
                return await GoToEntryAsync(item, cancellationToken, []);

            var supplied = Snapshot(item.Arguments);
            if (args is not null)
            {
                // By name, as the values: a parameter of the caller replaces the one of the entry that has its name (the constructor takes the first
                // of a name and the property the last: both must see the same one).
                foreach (var parameter in args.ViewModelParameters)
                    RouteParser.SetValue(supplied.ViewModelParameters, parameter);
                foreach (var parameter in args.ViewParameters)
                    RouteParser.SetValue(supplied.ViewParameters, parameter);
                foreach (var value in args.Values)
                    RouteParser.SetValue(supplied.Values, value);    // a value of the caller replaces the one of the entry that has its name
            }
            // The address asked for is the route of the change (history, URL of the browser), query included; matching it adds the query values.
            return await GoToEntryAsync(item, cancellationToken, [], supplied, url);
        }

        // Nothing registered at all is the usual first mistake: say where to look.
        var hint = entries.Any()
            ? string.Empty
            : " No route is registered at all: check that the projects with the [Routeable] view models and the views reference the Routeables package (it carries the source generator). An executable registers its generated routes by itself; a library, or a project built with RouteablesAutoRegister=false, needs its AddGeneratedRoutes() called.";
        // A page for the addresses nobody serves (UseNotFoundRoute), unless the missing one is that page itself.
        if (provider.GetService<RouteableNotFound>() is { } notFound && !RouteAddress.SamePath(notFound.Route, url))
        {
            // A copy: the caller's args must not collect a notFoundUrl per missing address.
            var shown = Snapshot(args);
            RouteParser.SetValue(shown.Values, new RouteableArgument(url, RouteableNotFound.UrlValue));
            return await GoToAsync(notFound.Route, shown, null, cancellationToken);
        }

        // A template written with a placeholder that the parser reads as fixed text is the likely reason a registered route does not match.
        if (entries.Select(e => (e.Template, Piece: RouteParser.LooksLikeAPlaceholder(e.Template))).FirstOrDefault(e => e.Piece is not null) is { Piece: { } piece } suspect)
            hint += $" The registered template '{suspect.Template}' has '{piece}', which is read as fixed text: a placeholder is written {{name}}, {{name:type}} or {{name:type?}} (optional), and in a query {{name}} on its own, not name={{name}}.";

        return RouteableResult.Fail(new InvalidOperationException($"No route matches '{url}'. Register it with an explicit route, for instance RegisterRoute<TView, TViewModel>(route: \"/orders/{{id:int}}\")." + hint));
    }

    // What a route is called in the telemetry: its template, never the address (an address holds identifiers, and one series per address is unbounded).
    // A route that no template names (a direct route change, a menu entry without a template) is "(unmatched)": the address itself would put its identifiers in the tag.
    private string RouteTag(string? route)
        => string.IsNullOrWhiteSpace(route) ? string.Empty : TryTemplateOf(route, out var template) ? PathOf(template) : UnmatchedRouteTag;

    internal const string UnmatchedRouteTag = "(unmatched)";

    // The address without its query and its fragment (both can hold a token: "#access_token=" of an implicit OAuth flow).
    internal static string PathOf(string? route)
    {
        var address = route ?? string.Empty;
        var end = address.AsSpan().IndexOfAny('?', '#');
        return end < 0 ? address : address[..end];
    }

    // The template registered for a route: the one that is the route, the one that matches it, the one of a menu entry, or the route itself.
    private string TemplateOf(string route) => TryTemplateOf(route, out var template) ? template : route;

    string? IRouteTemplates.TemplateOf(string route) => TryTemplateOf(route, out var template) ? template : null;

    private bool TryTemplateOf(string route, out string template)
    {
        var entries = provider.GetService<RouteableTable>()?.Entries ?? [];
        // The template that is the route itself (query included: "/search" and "/search?mode=grid" are two routes), then the one that matches it,
        // then the one that has its path.
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
