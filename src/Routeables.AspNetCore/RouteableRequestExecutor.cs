using Routeables.AspNetCore.Rendering;
using Routeables.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore
{
    public interface IRouteableRequestExecutor
    {
        Task<RouteableRequestResult> NavigateAsync(
            string route,
            CancellationToken cancellationToken = default);

        Task<RouteableRequestResult> NavigateAsync(
            RouteableRequestContext context);
    }
    public sealed class RouteableRequestExecutor(
    Routeables.Abstraction.IRouter router,
    IServiceProvider services,
    IRouteableViewResolver viewResolver)
    : IRouteableRequestExecutor
    {
        public async Task<RouteableRequestResult> NavigateAsync(
            string route,
            CancellationToken cancellationToken = default)
        {
            var result = await router.GoToAsync(
                route,
                RouteableArgs.Create(),
                cancellationToken: cancellationToken);

            if (!result.Success)
                return RouteableRequestResult.Failed(result.Exception);

            var viewModel = router.CurrentViewModel;

            if (viewModel is null)
                return RouteableRequestResult.NotFound();

            var view = await viewResolver.ResolveAsync(
                viewModel.GetType(),
                cancellationToken);

            if (view is null)
                return RouteableRequestResult.ViewNotFound(
                    viewModel.GetType());

            return RouteableRequestResult.Succeeded(view, viewModel);
        }

        public Task<RouteableRequestResult> NavigateAsync(
            RouteableRequestContext context)
        {
            return NavigateAsync(
                context.Route,
                context.CancellationToken);
        }
    }
}
