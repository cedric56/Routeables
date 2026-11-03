using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

/// <summary>
/// Registers the dialog, content dialog, task dialog and notification services of an Avalonia application.
/// <c>AppBuilder.UseRouteables</c> already calls <see cref="UseRouteablesServices"/> with its defaults: call it again only to give an icon
/// or another <see cref="IMessageBoxService"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers <see cref="IRouteableDialogService"/> (message boxes, content dialogs and task dialogs), <see cref="IRouteableNotificationService"/>
    /// and the <see cref="IMessageBoxService"/> that draws them. <c>UseRouteables</c> already calls it; call it again from its <c>configure</c> callback only for an icon other than the one of the main window,
    /// or another message box service. A second call replaces the first.
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;().UseRouteables(services => services.UseRouteablesServices(icon: "avares://MyApp/Assets/icon.ico"));
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="messageBoxService">Builds the <see cref="IMessageBoxService"/> that draws the dialogs and the notifications; <see cref="DefaultMessageBoxService"/> when not set.
    /// <param name="notificationService">Builds the <see cref="IRouteableNotificationService"/>; Routeables' own when not set.</param>
    /// Give another one to draw them another way, such as the FluentAvalonia one of the Reshapes.</param>
    /// <param name="icon">The icon of the dialog windows drawn by <see cref="DefaultMessageBoxService"/>; null gives them the icon of the window that opens them.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection UseRouteablesServices(
        this IServiceCollection services,
        Func<IServiceProvider, IMessageBoxService>? messageBoxService = null,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService = null,
        string? icon = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IRouteableNotificationService>();
        services.RemoveAll<IRouteableDialogService>();
        services.RemoveAll<IMessageBoxService>();

        services.AddTransient(notificationService ?? (sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>())));
        services.AddSingleton<IRouteableDialogService, RouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IMessageBoxService>(messageBoxService ?? (_ => new DefaultMessageBoxService(icon)));
        return services;
    }
}
