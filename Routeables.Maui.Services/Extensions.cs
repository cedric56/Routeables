using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// Registers the modal, content dialog, task dialog, dialog and notification services of a MAUI application.
/// <c>UseRouteables&lt;TMain&gt;()</c> (Routeables.Maui) registers the router only; this package is for applications that want the UI services too.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers <see cref="IRouteableDialogService"/>, <see cref="IRouteableModalService"/>, <see cref="IRouteableContentDialogService"/>,
    /// <see cref="IRouteableTaskDialogService"/>, <see cref="IRouteableNotificationService"/> and the
    /// <see cref="IRouteableDialogWindowContext"/> they draw on.
    /// </summary>
    /// <remarks>
    /// They are Routeables' own and need nothing but MAUI. Pass a factory to replace one, or call
    /// <c>UseCommunityToolkit()</c> (Reshapes.Maui.CommunityToolkit) to use CommunityToolkit.Maui popups and snackbars instead.
    /// </remarks>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="modalService">Factory of the <see cref="IRouteableModalService"/>; null keeps Routeables' own.</param>
    /// <param name="contentDialogService">Factory of the <see cref="IRouteableContentDialogService"/>; null keeps Routeables' own.</param>
    /// <param name="taskDialogService">Factory of the <see cref="IRouteableTaskDialogService"/>; null keeps Routeables' own.</param>
    /// <param name="notificationService">Factory of the <see cref="IRouteableNotificationService"/>; null keeps Routeables' own.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteablesServices(
        this MauiAppBuilder builder,
        Func<IServiceProvider, IRouteableModalService>? modalService = null,
        Func<IServiceProvider, IRouteableContentDialogService>? contentDialogService = null,
        Func<IServiceProvider, IRouteableTaskDialogService>? taskDialogService = null,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRouteableDialogs();
        builder.Services.AddSingleton<IRouteableDialogService, RouteableDialogService>();
        builder.Services.AddSingleton<RouteableDialogService>(sp => (RouteableDialogService)sp.GetRequiredService<IRouteableDialogService>());
        RegisterOrDefault<IRouteableModalService, RouteableModalService>(builder.Services, modalService);
        builder.Services.AddSingleton<IRouteableDialogWindowContext, RouteableDialogWindowContext>();
        RegisterOrDefault<IRouteableContentDialogService, RouteableContentDialogService>(builder.Services, contentDialogService);
        RegisterOrDefault<IRouteableTaskDialogService, RouteableTaskDialogService>(builder.Services, taskDialogService);
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
        if (factory is null)
            services.AddSingleton<TService, TImplementation>();
        else
            services.AddSingleton(factory);
    }
}
