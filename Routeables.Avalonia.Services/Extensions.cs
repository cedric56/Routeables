using Microsoft.Extensions.DependencyInjection;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

/// <summary>
/// Registers the dialog, content dialog, task dialog and notification services of an Avalonia application.
/// <c>AppBuilder.UseRouteables</c> (Routeables.Avalonia) builds the container with the router only; this package is for applications
/// that want the UI services too.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers <see cref="IRouteableDialogService"/>, <see cref="IRouteableContentDialogService"/>,
    /// <see cref="IRouteableTaskDialogService"/>, <see cref="IRouteableNotificationService"/> and the <see cref="IMessageBoxService"/>
    /// that draws them. Call it from the <c>configureExternalServices</c> callback of <c>UseRouteables</c>.
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;().UseRouteables(services => services.UseRouteablesServices("avares://MyApp/Assets/icon.ico"));
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="icon">The icon of the dialog windows drawn by <see cref="DefaultMessageBoxService"/>.</param>
    /// <param name="messageBoxService">Optional replacement of <see cref="DefaultMessageBoxService"/>, such as the FluentAvalonia one of the Reshapes.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection UseRouteablesServices(
        this IServiceCollection services,
        string icon,
        IMessageBoxService? messageBoxService = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IRouteableNotificationService, RouteableNotificationService>(sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableContentDialogService, RouteableContentDialogService>(sp => new RouteableContentDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableTaskDialogService, RouteableTaskDialogService>(sp => new RouteableTaskDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton<IRouteableDialogService, RouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));
        services.AddSingleton(messageBoxService ?? new DefaultMessageBoxService(icon));
        services.AddRouteableDialogs();
        return services;
    }
}
