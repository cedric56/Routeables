using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.AspNetCore;

//public abstract class RazorPageBase : IRazorPage
//{
//    protected RazorPageBase(
//        IRouter router,
//        IRouteableRequestExecutor executor)
//    {
//        Router = router;
//        Executor = executor;
//    }
//    protected IRouter Router { get; }
//    protected IRouteableRequestExecutor Executor { get; }
//    public abstract string Path { get; }
//    public ViewContext ViewContext { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//    public IHtmlContent? BodyContent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//    public bool IsLayoutBeingRendered { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//    string IRazorPage.Path { get => Path; set => throw new NotImplementedException(); }
//    public string? Layout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//    public IDictionary<string, RenderAsyncDelegate> PreviousSectionWriters { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

//    public IDictionary<string, RenderAsyncDelegate> SectionWriters => throw new NotImplementedException();

//    public abstract Task ExecuteAsync();

//    public void EnsureRenderedBodyOrSections()
//    {
//        throw new NotImplementedException();
//    }
//}
///// <summary>
///// 
///// </summary>
///// <typeparam name="TModel"></typeparam>
//public abstract class RazorPage<TModel>(IRouter router,
//        IRouteableRequestExecutor executor) : RazorPageBase(router, executor)
//{
//    public TModel Model { get; }   // your @model
//}
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
        string route, string template)
    {
        var result = await Executor.NavigateAsync(
            route,
            template,
            HttpContext.RequestAborted);

        return result.ToActionResult(PageContext);
    }
}