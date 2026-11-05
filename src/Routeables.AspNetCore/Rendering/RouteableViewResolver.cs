using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore.Rendering
{
    public interface IRouteableViewResolver
    {
        Task<RouteableViewDescriptor?> ResolveAsync(
            Type viewModelType,
            CancellationToken cancellationToken = default);
    }
    public sealed class RouteableViewResolver
    : IRouteableViewResolver
    {
        public Task<RouteableViewDescriptor?> ResolveAsync(
            Type viewModelType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(viewModelType);

            // À remplacer par la résolution réelle du moteur de vues
            // ASP.NET Core, en utilisant les conventions de ton projet.

            return Task.FromResult<RouteableViewDescriptor?>(
                null);
        }
    }
}
