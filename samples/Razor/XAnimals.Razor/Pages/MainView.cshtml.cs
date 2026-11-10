using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Routeables.Abstraction.Bases;
using Routeables.AspNetCore;
using XAnimals.Common;
using XAnimals.Razor.Session;
using IRouter = Routeables.Abstraction.Routing.IRouter;

namespace XAnimals.Razor.Pages;

/// <summary>
/// The only page of the site: its address is a route, and the view of that route is drawn inside it. The router and the view models are those
/// of the session of the visitor, so they are still there on the next request: a button posts, the page does what was asked, and the
/// visitor is sent back to a GET of the page that is now shown.
/// </summary>
public class MainViewPageModel(IRouteableMenuViewModel main, IRouter router, IServiceProvider provider, RouteableInteractions interactions, RouteablePageState state) : PageModel
{
    /// <summary>The entries of the menu that have no view in this sample: the Birds module has views for the other platforms only.</summary>
    private static readonly string[] Unserved = ["/birds"];

    /// <summary>The menu view-model of the application.</summary>
    public IRouteableMenuViewModel Main => main;

    /// <summary>The router that owns the current page and its history.</summary>
    public IRouter Router => router;

    /// <summary>Dialogs and toasts waiting for the visitor.</summary>
    public RouteableInteractions Interactions => interactions;

    /// <summary>The address matches no route: the page says so, and the state of the visitor is left alone.</summary>
    public new bool NotFound { get; private set; }

    /// <summary>The URL that was last successfully served for this session.</summary>
    public new string Url => state.ServedUrl ?? "/";

    /// <summary>The entries the menu links to.</summary>
    public IEnumerable<IRouteableMenuEntry> MenuEntries => main.Entries.Where(e => !Unserved.Contains(Address(e.Route), StringComparer.OrdinalIgnoreCase));

    /// <summary>Turns a route into an absolute path starting with <c>/</c>, never with <c>//</c> or <c>/\</c> (see <see cref="RouteableAddress.Of"/>).</summary>
    public static string Address(string route) => RouteableAddress.Of(route);

    /// <summary>
    /// Serves the page for the given path. Reloads keep the current view; a new address navigates.
    /// </summary>
    public async Task OnGetAsync(string? path)
    {
        var url = "/" + (path?.TrimStart('/') ?? "") + Request.QueryString.Value;

        interactions.Expire();

        // The same address again (a reload, the redirect after a button): the page as it is. Another one starts it.
        if (state.ServedUrl == url && router.CurrentView is not null)
            return;

        if (await NavigateAsync(url))
            state.ServedUrl = url;
        else
        {
            // A request for nothing (a favicon, a probe of a tool) must not take the visitor away from the page that is open.
            NotFound = true;
            Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }

    /// <summary>
    /// Handles a button post: runs the requested action, then redirects to the page that is now shown.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(string? path)
    {
        var form = Request.Form;
        var before = router.CurrentViewModel;

        await PageActions.RunAsync(provider, form["target"].ToString(), form["do"].ToString(), form["value"].ToString());

        // A button can move the router (Open Members, Sign out): the address follows the page that is shown.
        var url = Address(path ?? "") + Request.QueryString.Value;
        if (!ReferenceEquals(before, router.CurrentViewModel) && router.History.Current?.Args.Route is { Length: > 0 } route)
            url = Address(route);

        state.ServedUrl = url;
        // LocalRedirect refuses an address of another site, and the base path of the application is put in front when it has one.
        return LocalRedirect(RouteableAddress.Of(url, Request.PathBase));
    }

    /// <summary>
    /// Navigates the router to <paramref name="url"/>. Returns <see langword="false"/> when the address is unknown.
    /// </summary>
    private async Task<bool> NavigateAsync(string url)
    {
        var result = await router.GoToAsync(url);

        // The home address has no route: the application opens its first entry, once.
        if (!result.Success && url == "/")
        {
            main.SelectFirst();
            return true;
        }

        if (!result.Success)
            return false;

        // The search of a list comes from the address (/cats?SearchText=tom): nothing keeps the view model between two GET of another address.
        if (router.CurrentViewModel is AnimalsViewModel list
            && Request.Query.TryGetValue("SearchText", out var searchText))
            list.SearchText = searchText.ToString();

        await PageActions.ShownAsync(provider, router.CurrentViewModel);
        return true;
    }

    /// <summary>The name of the partial view of a view: its class is named after its path (<c>Views_CatsView</c> is <c>~/Views/CatsView.cshtml</c>).</summary>
    public static string ViewName(object view)
        => "~/" + view.GetType().Name.Replace('_', '/') + ".cshtml";
}