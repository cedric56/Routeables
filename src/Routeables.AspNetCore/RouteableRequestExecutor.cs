
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Routeables.AspNetCore.Rendering;
using Routeables.Routing;

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
                route,
                cancellationToken: cancellationToken);

            if (!result.Success)
                return RouteableRequestResult.Failed(result.Exception);

            var viewModel = router.CurrentViewModel;

            if (viewModel is null)
                return RouteableRequestResult.NotFound();

            var viewModelType = viewModel.GetType();

            var view = await viewResolver.ResolveAsync(
                viewModelType,
                cancellationToken);

            if (view is null)
            {
                return RouteableRequestResult.ViewNotFound(
                    viewModelType);
            }

            return RouteableRequestResult.Succeeded(
                view,
                viewModel);
        }

        public Task<RouteableRequestResult> NavigateAsync(
            RouteableRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return NavigateAsync(
                context.Route,
                context.CancellationToken);
        }
    }
}
