global using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;
global using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
global using Button = Microsoft.UI.Xaml.Controls.Button;
global using Application = Microsoft.UI.Xaml.Application;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>
/// Registers the dialog, content dialog, task dialog and notification services of an Uno Platform application.
/// <c>Application.UseRouteables</c> (Routeables.Uno) builds the container with the router only; this package is for applications
/// that want the UI services too.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers <see cref="IRouteableDialogService"/>, <see cref="IRouteableContentDialogService"/>,
    /// <see cref="IRouteableTaskDialogService"/>, <see cref="IRouteableNotificationService"/> and the <see cref="IMessageBoxService"/>
    /// that draws them. Call it from the <c>configure</c> callback of <c>UseRouteables</c>.
    /// <code>
    /// Services = this.UseRouteables(services => services.UseRouteablesServices());
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

        services.AddSingleton(messageBoxService ?? new DefaultMessageBoxService());
        services.AddSingleton<IRouteableNotificationService>(sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableContentDialogService>(sp => new RouteableContentDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableTaskDialogService>(sp => new RouteableTaskDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));
        return services;
    }
}
