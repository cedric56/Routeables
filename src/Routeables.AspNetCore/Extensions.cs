using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.AspNetCore.Rendering;
using Routeables.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore
{
    public static class Extensions
    {
        public static IServiceCollection
            AddRouteablesAspNetCore<TMain>(
                this IServiceCollection services,
                Action<IServiceCollection>? configure = null)
            where TMain : class, IMainViewModel
        {
            services.AddScoped(
                _ => new RouteableHostLifetime(
                    ServiceLifetime.Scoped));

            services.RegisterRouting(
                ServiceLifetime.Scoped);

            services.RegisterMainViewModel<TMain>();

            services.AddScoped<
                IRouteableRequestExecutor,
                RouteableRequestExecutor>();

            services.AddScoped<
                IRouteableViewResolver,
                RouteableViewResolver>();

            //services.AddScoped<
            //    IRouteableViewRenderer,
            //    RouteableViewRenderer>();

            configure?.Invoke(services);

            return services;
        }
    }
}
