
using Microsoft.AspNetCore.Mvc.Razor;

namespace Routeables.AspNetCore.Rendering
{
    public interface IRouteableViewResolver
    {
        Task<RouteableViewDescriptor?> ResolveAsync(
            Type viewModelType,
            CancellationToken cancellationToken = default);
    }

    public sealed class RouteableViewResolver : IRouteableViewResolver
    {
        private readonly IRazorViewEngine _viewEngine;

        public RouteableViewResolver(IRazorViewEngine viewEngine)
        {
            _viewEngine = viewEngine;
        }

        public Task<RouteableViewDescriptor?> ResolveAsync(
            Type viewModelType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(viewModelType);

            cancellationToken.ThrowIfCancellationRequested();

            // HomeViewModel => Home
            var viewFolder = viewModelType.Name;

            const string suffix = "ViewModel";

            if (viewFolder.EndsWith(
                suffix,
                StringComparison.Ordinal))
            {
                viewFolder = viewFolder[..^suffix.Length];
            }

            // ~/Views/Home/Index.cshtml
            var viewPath =
                $"~/Views/{viewFolder}/Index.cshtml";

            var result = _viewEngine.GetView(
                executingFilePath: null,
                viewPath: viewPath,
                isMainPage: true);

            if (!result.Success)
            {
                return Task.FromResult<RouteableViewDescriptor?>(
                    null);
            }

            var descriptor = new RouteableViewDescriptor(
                result.View.Path,
                viewModelType);

            return Task.FromResult<RouteableViewDescriptor?>(
                descriptor);
        }
    }
}
