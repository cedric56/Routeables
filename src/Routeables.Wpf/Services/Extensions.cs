using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>Registers the dialog and notification services of the WPF host.</summary>
public static class Extensions
{
    /// <summary>
    /// Registers the dialog, content dialog, task dialog and notification services. <c>Application.UseRouteables</c> already
    /// calls it; call it again in its <c>configure</c> to give an icon or your own <see cref="IMessageBoxService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="messageBoxService">Builds the <see cref="IMessageBoxService"/> that draws the dialogs and the notifications; <see cref="DefaultMessageBoxService"/> when not set.</param>
    /// <param name="notificationService">Builds the <see cref="IRouteableNotificationService"/>; Routeables' own when not set.</param>
    /// <param name="icon">The icon of the dialog windows (a pack URI or a file); the one of the active window when not set.</param>
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
