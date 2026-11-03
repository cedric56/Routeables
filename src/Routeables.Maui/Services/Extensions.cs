using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;
#if ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif

namespace Routeables.Maui.Services;

/// <summary>
/// Registers the modal, content dialog, task dialog, dialog and notification services of a MAUI application.
/// <c>UseRouteables&lt;TMain&gt;()</c> (Routeables.Maui) already calls <see cref="UseRouteablesServices"/> with its defaults: call it again only to replace a service.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers 
    /// <see cref="IRouteableDialogService"/>,
    /// <see cref="IRouteableNotificationService"/> and the
    /// <see cref="IRouteableDialogWindowContext"/> they draw on.
    /// </summary>
    /// <remarks>
    /// <c>UseRouteables</c> already calls it. They are Routeables' own and need nothing but MAUI. A second call replaces the first: pass a factory to replace one, or call
    /// <c>UseCommunityToolkit()</c> (Reshapes.Maui.CommunityToolkit) to use CommunityToolkit.Maui popups and snackbars instead.
    /// </remarks>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="messageBoxService">Factory of the <see cref="IMessageBoxService"/>; null keeps Routeables' own.</param>
    /// <param name="notificationService">Factory of the <see cref="IRouteableNotificationService"/>; null keeps Routeables' own.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteablesServices(
        this MauiAppBuilder builder,
        Func<IServiceProvider, IMessageBoxService>? messageBoxService = null,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.RemoveAll<IRouteableDialogService>();
        builder.Services.RemoveAll<RouteableDialogService>();
        builder.Services.RemoveAll<IRouteableDialogWindowContext>();
        builder.Services.AddSingleton<IRouteableDialogService>(sp => new RouteableDialogService(
#if ANDROID || WINDOWS || IOS || MACCATALYST
            sp.GetRequiredService<IServiceProvider>(),
#endif
            sp.GetRequiredService<IRouteableDialogWindowContext>(),
            messageBoxService is null ? null : sp.GetRequiredService<IMessageBoxService>()));
        builder.Services.AddSingleton<RouteableDialogService>(sp => (RouteableDialogService)sp.GetRequiredService<IRouteableDialogService>());
        // The message box service is the caller's, or Routeables' own native one (the dialog service itself), which also shows the modals.
        builder.Services.RemoveAll<IMessageBoxService>();
        if (messageBoxService is null)
            builder.Services.AddSingleton<IMessageBoxService>(sp => sp.GetRequiredService<RouteableDialogService>());
        else
            builder.Services.AddSingleton(messageBoxService);
        builder.Services.AddSingleton<IRouteableDialogWindowContext, RouteableDialogWindowContext>();
        builder.Services.RemoveAll<RouteableSystemNotifications>();
        builder.Services.AddSingleton(new RouteableSystemNotifications());
        RegisterOrDefault<IRouteableNotificationService, RouteableNotificationService>(builder.Services, notificationService);
        return builder;
    }

    // The factory when the application gave one, Routeables' own implementation otherwise.
    private static void RegisterOrDefault<TService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        IServiceCollection services,
        Func<IServiceProvider, TService>? factory)
        where TService : class
        where TImplementation : class, TService
    {
        services.RemoveAll<TService>();
        if (factory is null)
            services.AddSingleton<TService, TImplementation>();
        else
            services.AddSingleton(factory);
    }

    /// <summary>
    /// Turns on the system notifications of <see cref="IRouteableNotificationService"/> (<c>CreateNotificationAsync</c>, <c>ActiveNotifications</c>,
    /// <c>CloseAll</c>, <c>NotificationCompleted</c>, <c>InitializeAsync</c>): Android, iOS, Mac Catalyst and Windows use their notification center.
    /// It is the counterpart of <c>WithAppNotifications</c> of Avalonia.Labs.Notifications, and separate from <c>UseRouteables</c>: without this call the
    /// service creates none. On Android it also registers the lifecycle events that bring a tap on a notification back.
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="options">The <see cref="AppNotificationOptions"/>; the defaults when <see langword="null"/>.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder WithAppNotifications(this MauiAppBuilder builder, AppNotificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        SystemNotificationCenter.Current.Configure(options ?? new AppNotificationOptions());
#if ANDROID
        // A tap on a system notification starts or comes back to the activity with the id of the notification as an extra.
        builder.ConfigureLifecycleEvents(events => events.AddAndroid(android => android
            .OnCreate((activity, _) => SystemNotificationCenter.HandleIntent(activity.Intent))
            .OnNewIntent((_, intent) => SystemNotificationCenter.HandleIntent(intent))));
#endif
        return builder;
    }
}
