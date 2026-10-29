using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// Registers the dialogs, content dialogs, task dialogs, modals and notifications of a Blazor application.
/// <c>UseRouteablesServer</c>, <c>UseRouteablesWebAssembly</c> and <c>UseRouteablesHybrid</c> already call <see cref="UseRouteablesServices"/> with its
/// defaults: call it again only to replace a service or to move the notifications.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers the dialog, content dialog, task dialog, modal and notification services; <c>UseRouteablesServer</c> already calls it,
    /// and a second call replaces the first. Display them with the hosts
    /// of <c>MainLayout</c>, or place <c>RouteableDialogHost</c>, <c>RouteableContentDialogHost</c>, <c>RouteableTaskDialogHost</c>,
    /// <c>RouteableModalHost</c> and <c>RouteableNotificationHost</c> once in your own layout.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="routeableDialogService">Factory of the <see cref="IRouteableDialogService"/>; null keeps Routeables' own.</param>
    /// <param name="routeableContentDialogService">Factory of the <see cref="IRouteableContentDialogService"/>; null keeps Routeables' own.</param>
    /// <param name="routeableTaskDialogService">Factory of the <see cref="IRouteableTaskDialogService"/>; null keeps Routeables' own.</param>
    /// <param name="routeableNotificationService">Factory of the <see cref="IRouteableNotificationService"/>; null keeps Routeables' own.</param>
    /// <param name="position">The screen position where toast notifications appear, bottom-left by default.</param>
    /// <param name="maxDispayments">The maximum number of notifications displayed at once, 5 by default.</param>
    public static IServiceCollection UseRouteablesServices(this IServiceCollection services,
        Func<IServiceProvider, IRouteableDialogService>? routeableDialogService = null,
        //Func<IServiceProvider, IRouteableContentDialogService>? routeableContentDialogService = null,
        //Func<IServiceProvider, IRouteableTaskDialogService>? routeableTaskDialogService = null,
        Func<IServiceProvider, IRouteableNotificationService>? routeableNotificationService = null,
        RouteableNotificationPosition position = RouteableNotificationPosition.BottomLeft,
        int maxDispayments = 5)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<RouteableModalService>();
        services.RemoveAll<RouteableNotificationOptions>();
        services.AddScoped<RouteableModalService>();
        services.RegisterServices<IRouteableDialogService, RouteableDialogService>(
            sp => new RouteableDialogService(sp),
            routeableDialogService);
        services.AddScoped<RouteableContentDialogQueue>();
        services.AddScoped<RouteableTaskDialogQueue>();
        //services.RegisterServices<IRouteableContentDialogService, RouteableContentDialogService>(routeableContentDialogService);
        //services.RegisterServices<IRouteableTaskDialogService, RouteableTaskDialogService>(routeableTaskDialogService);
        services.AddSingleton(new RouteableNotificationOptions
        {
            Position = position,
            MaxDisplayed = maxDispayments
        });
        services.RegisterServices<IRouteableNotificationService, RouteableNotificationService>(
            sp => new RouteableNotificationService(sp.GetRequiredService<RouteableNotificationOptions>()),
            routeableNotificationService);
        return services;
    }

    private static void RegisterServices<TInterface, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TInterface>? factory = null)
        where TImplementation : class, TInterface, new()
        where TInterface : class
        => services.RegisterServices<TInterface, TImplementation>(_ => new TImplementation(), factory);

    private static void RegisterServices<TInterface, TImplementation>(this IServiceCollection services,
        Func<IServiceProvider, TImplementation> implementationFactory,
        Func<IServiceProvider, TInterface>? factory = null)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.RemoveAll<TInterface>();
        services.RemoveAll<TImplementation>();
        Func<IServiceProvider, TInterface>? service = factory;
        if (service == null)
        {
            service = sp => implementationFactory(sp);
            services.AddScoped(sp => (TImplementation)sp.GetRequiredService<TInterface>());
        }
        services.AddScoped(service);
    }
}
