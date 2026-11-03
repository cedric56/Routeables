global using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;
global using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
global using Button = Microsoft.UI.Xaml.Controls.Button;
global using Application = Microsoft.UI.Xaml.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>
/// Registers the dialog, content dialog, task dialog and notification services of an Uno Platform application.
/// <c>Application.UseRouteables</c> already calls <see cref="UseRouteablesServices"/> with its defaults: call it again only to give
/// another <see cref="IMessageBoxService"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers <see cref="IRouteableDialogService"/>, <see cref="IRouteableNotificationService"/> and the <see cref="IMessageBoxService"/>
    /// that draws them. <c>UseRouteables</c> already calls it; call it again from its <c>configure</c> callback to give another message box
    /// service. A second call replaces the first.
    /// <code>
    /// Services = this.UseRouteables&lt;MainViewModel&gt;(services => services.UseRouteablesServices(sp => new MyMessageBoxService()));
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="messageBoxService">Builds the <see cref="IMessageBoxService"/> that draws the dialogs and the notifications; <see cref="DefaultMessageBoxService"/> when not set.</param>
    /// <param name="notificationService">Builds the <see cref="IRouteableNotificationService"/>; Routeables' own when not set.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection UseRouteablesServices(
        this IServiceCollection services,
        Func<IServiceProvider, IMessageBoxService>? messageBoxService = null,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IMessageBoxService>();
        services.RemoveAll<IRouteableNotificationService>();
        services.RemoveAll<IRouteableDialogService>();

        services.AddSingleton<IMessageBoxService>(messageBoxService ?? (_ => new DefaultMessageBoxService()));
        services.AddSingleton(notificationService ?? (sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>())));
        services.AddSingleton<IRouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));
        return services;
    }
}
