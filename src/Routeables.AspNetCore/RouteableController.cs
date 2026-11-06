using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Routeables.Abstraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore
{
    public interface IRouteableView
    {
        Task RenderAsync(ViewContext context, object viewmodel);
    }

    public abstract class RouteableController : Controller
    {
        protected RouteableController(
            IRouter router,
            IRouteableRequestExecutor executor)
        {
            Router = router;
            Executor = executor;
        }

        protected IRouter Router { get; }

        protected IRouteableRequestExecutor Executor { get; }
    }
}
