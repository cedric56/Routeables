using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.AspNetCore.Pages;

public abstract class RouteablePageModel : PageModel
{
    protected RouteablePageModel(
        IRouter router,
        IRouteableRequestExecutor executor)
    {
        Router = router;
        Executor = executor;
    }

    protected IRouter Router { get; }

    protected IRouteableRequestExecutor Executor { get; }

    protected async Task<IActionResult> NavigateAsync(
        string route)
    {
        var result = await Executor.NavigateAsync(
            route,
            HttpContext.RequestAborted);

        return result.ToActionResult(PageContext);
    }
}