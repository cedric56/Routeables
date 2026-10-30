using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;

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
            messageBoxService?.Invoke(sp)));
        builder.Services.AddSingleton<RouteableDialogService>(sp => (RouteableDialogService)sp.GetRequiredService<IRouteableDialogService>());
        builder.Services.AddSingleton<IRouteableDialogWindowContext, RouteableDialogWindowContext>();
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
}
