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
    /// Registers <see cref="IRouteableDialogService"/>, <see cref="IRouteableContentDialogService"/>,
    /// <see cref="IRouteableTaskDialogService"/>, <see cref="IRouteableNotificationService"/> and the <see cref="IMessageBoxService"/>
    /// that draws them. <c>UseRouteables</c> already calls it; call it again from its <c>configure</c> callback to give another message box
    /// service. A second call replaces the first.
    /// <code>
    /// Services = this.UseRouteables&lt;MainViewModel&gt;(services => services.UseRouteablesServices(new MyMessageBoxService()));
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="messageBoxService">Optional replacement of <see cref="DefaultMessageBoxService"/>.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection UseRouteablesServices(
        this IServiceCollection services,
        IMessageBoxService? messageBoxService = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IMessageBoxService>();
        services.RemoveAll<IRouteableNotificationService>();
        services.RemoveAll<IRouteableContentDialogService>();
        services.RemoveAll<IRouteableTaskDialogService>();
        services.RemoveAll<IRouteableDialogService>();

        services.AddSingleton(messageBoxService ?? new DefaultMessageBoxService());
        services.AddSingleton<IRouteableNotificationService>(sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableContentDialogService>(sp => new RouteableContentDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableTaskDialogService>(sp => new RouteableTaskDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));
        return services;
    }
}
