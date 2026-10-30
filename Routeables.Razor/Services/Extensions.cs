using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// Registers the dialogs (message boxes, content dialogs and task dialogs), modals and notifications of a Blazor application.
/// <c>UseRouteablesServer</c>, <c>UseRouteablesWebAssembly</c> and <c>UseRouteablesHybrid</c> already call <see cref="UseRouteablesServices"/> with its
/// defaults: call it again only to replace a service or to move the notifications.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers the <see cref="IRouteableDialogService"/> (message boxes, content dialogs and task dialogs), modal and notification
    /// services, and the <see cref="IMessageBoxService"/> that draws the dialogs; <c>UseRouteablesServer</c> already calls it,
    /// and a second call replaces the first. Display them with the hosts
    /// of <c>MainLayout</c>, or place <c>RouteableDialogHost</c>, <c>RouteableContentDialogHost</c>, <c>RouteableTaskDialogHost</c>,
    /// <c>RouteableModalHost</c> and <c>RouteableNotificationHost</c> once in your own layout.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="messageBoxService">Builds the <see cref="IMessageBoxService"/> that draws the dialogs; <see cref="DefaultMessageBoxService"/> (plain Razor, shown by the hosts of the layout) when not set. Give another one to draw them with a UI library, such as the MudBlazor one of the Reshapes.</param>
    /// <param name="notificationService">Factory of the <see cref="IRouteableNotificationService"/>; null keeps Routeables' own.</param>
    /// <param name="position">The screen position where toast notifications appear, bottom-left by default.</param>
    /// <param name="maxDispayments">The maximum number of notifications displayed at once, 5 by default.</param>
    public static IServiceCollection UseRouteablesServices(this IServiceCollection services,
        Func<IServiceProvider, IMessageBoxService>? messageBoxService = null,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService = null,
        RouteableNotificationPosition position = RouteableNotificationPosition.BottomLeft,
        int maxDispayments = 5)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<RouteableModalService>();
        services.RemoveAll<RouteableNotificationOptions>();
        services.AddScoped<RouteableModalService>();

        services.RemoveAll<RouteableMessageQueue>();
        services.RemoveAll<RouteableContentDialogQueue>();
        services.RemoveAll<RouteableTaskDialogQueue>();
        services.AddScoped<RouteableMessageQueue>();
        services.AddScoped<RouteableContentDialogQueue>();
        services.AddScoped<RouteableTaskDialogQueue>();

        // The message box service draws the dialogs; one dialog service asks it, whatever draws them.
        services.RemoveAll<DefaultMessageBoxService>();
        services.RemoveAll<IMessageBoxService>();
        services.RemoveAll<IRouteableDialogService>();
        services.AddScoped(sp => new DefaultMessageBoxService(
            sp.GetRequiredService<RouteableMessageQueue>(),
            sp.GetRequiredService<RouteableContentDialogQueue>(),
            sp.GetRequiredService<RouteableTaskDialogQueue>()));
        services.AddScoped<IMessageBoxService>(messageBoxService ?? (sp => sp.GetRequiredService<DefaultMessageBoxService>()));
        services.AddScoped<IRouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));

        services.AddSingleton(new RouteableNotificationOptions
        {
            Position = position,
            MaxDisplayed = maxDispayments
        });
        services.RegisterServices<IRouteableNotificationService, RouteableNotificationService>(
            sp => new RouteableNotificationService(sp.GetRequiredService<RouteableNotificationOptions>()),
            notificationService);
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
