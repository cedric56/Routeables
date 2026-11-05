using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.AspNetCore.Rendering;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;
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
            services.RegisterMainViewModel<TMain>(ServiceLifetime.Scoped);
            services.AddRouteCoordinator(ServiceLifetime.Scoped);
            services.AddScoped<IRouteableRequestedThemeService>(_ => null!);
            services.AddScoped(
                _ => new RouteableHostLifetime(
                    ServiceLifetime.Scoped));

            services.RegisterRouting(
                ServiceLifetime.Scoped);

            services.AddScoped<
                IRouteableRequestExecutor,
                RouteableRequestExecutor>();

            services.AddScoped<
                IRouteableViewResolver,
                RouteableViewResolver>();
            services.AddSingleton<
    IRouteableViewModelStore,
    RouteableViewModelStore>();

            services.AddMemoryCache();

            services.AddSingleton<
                IRouteableViewModelStore,
                RouteableViewModelStore>();

            services.AddSingleton<RouteableViewModelOwner>();

            services.AddTransient<RouteableViewModelModelBinder>();

            //services.AddScoped<
            //    IRouteableViewRenderer,
            //    RouteableViewRenderer>();

            configure?.Invoke(services);

            return services;
        }
    }
}
