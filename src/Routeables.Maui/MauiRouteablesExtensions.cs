using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.LifecycleEvents;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Native;
using Routeables.Maui.Services;
using Routeables.Services;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Maui;

/// <summary>
/// Extension methods for wiring up the Routeables IoC container and its associated services
/// into a MAUI application's <see cref="MauiAppBuilder"/>.
/// </summary>
public static class MauiRouteablesExtensions
{
    /// <summary>
    /// <c>UseRouteables&lt;TMain&gt;</c> for an application that has no menu: no main view model to write. The first page is the one the
    /// <c>RouteableRegion</c> of the main page opens (<c>Route="/hello/World"</c>).
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseRouteables(r => r.AddGeneratedRoutes());
    /// </code>
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteables(this MauiAppBuilder builder, Action<IServiceCollection>? configure = null)
        => builder.UseRouteables<DefaultMainViewModel>(configure);

    /// <summary>
    /// Starts Routeables on MAUI: registers the router, the arguments factory, the property injection, the environment information and the
    /// theme service, and <typeparamref name="TMain"/> as the main view model (the menu), one for the application.
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseRouteables&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes());
    /// </code>
    /// </summary>
    /// <remarks>
    /// It registers the dialog (message boxes, content dialogs and task dialogs), modal and notification services too (<c>UseRouteablesServices()</c> of
    /// <c>Routeables.Maui.Services</c>); call that one again only to replace a service.
    /// </remarks>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this MauiAppBuilder builder, Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddRouteableHost(Runtime.Maui,
            new RouteablePropertyEntry(typeof(QueryPropertyAttribute), a => ((QueryPropertyAttribute)a).Name, FromContainer: false));
        builder.Services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        builder.Services.AddSingleton<IRouteableDispatcher, RouteableDispatcher>();
        builder.Services.AddSingleton<IRouteableViewBinder, RouteableViewBinder>();
        builder.Services.RegisterMainViewModel<TMain>();
        builder.UseRouteablesServices();
        configure?.Invoke(builder.Services);
        return builder;
    }

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
            sp,
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
