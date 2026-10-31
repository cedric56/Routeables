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
            sp => new RouteableNotificationService(sp.GetRequiredService<RouteableNotificationOptions>(), sp.GetService<BrowserSystemNotifications>()),
            notificationService);
        return services;
    }

    /// <summary>
    /// Turns on the system notifications of <see cref="IRouteableNotificationService"/> (<c>CreateNotificationAsync</c>, <c>ActiveNotifications</c>,
    /// <c>CloseAll</c>, <c>NotificationCompleted</c>, <c>InitializeAsync</c>): the notification center of the browser, through its Notification API.
    /// It is the counterpart of <c>WithAppNotifications</c> of Avalonia.Labs.Notifications, and separate from <c>UseRouteables*</c>: without this
    /// call the service creates none. Call it once at startup, before or after <see cref="UseRouteablesServices"/>.
    /// The user must grant the permission: <c>CreateNotificationAsync</c> asks for it when it was not asked yet (from a click) and returns
    /// <see langword="null"/> when it is denied, when the browser has no Notification API, or while the page is prerendered.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="options">The <see cref="AppNotificationOptions"/>; the defaults when <see langword="null"/>.</param>
    public static IServiceCollection WithAppNotifications(this IServiceCollection services, AppNotificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        options ??= new AppNotificationOptions();
        services.RemoveAll<AppNotificationOptions>();
        services.RemoveAll<BrowserSystemNotifications>();
        services.AddSingleton(options);
        services.AddScoped(sp => new BrowserSystemNotifications(
            sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(), sp.GetRequiredService<AppNotificationOptions>()));
        return services;
    }

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
