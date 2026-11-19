using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Services;
#if WPF
using Routeables.Wpf.Services;
#elif AVALONIA
using Routeables.Avalonia.Services;
#elif WINUI3
using Routeables.WinUI3.Services;
#else
using Routeables.Uno.Services;
#endif

// Shared source for every host: Routeables.Wpf, Routeables.Avalonia and Routeables.Maui (which define WPF, AVALONIA or MAUI), Routeables.Uno and
// Routeables.WinUI3. It holds what the hosts' extensions do the same way when the application starts and ends.
#if WPF
namespace Routeables.Wpf;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

internal static class RouteableHostStart
{
    /// <summary>
    /// Builds the container of the host: the router and the property injection, the theme service, the dispatcher and the view binder of the host,
    /// then what the application registers, and the routes the generator found. The locator serves it from now on.
    /// </summary>
    /// <param name="configure">Registers the routes and the services of the application.</param>
    /// <param name="afterBuild">Called with the container once it is built.</param>
    public static IServiceProvider Build(Action<IServiceCollection>? configure, Action<IServiceProvider>? afterBuild)
    {
        var services = new ServiceCollection();

#if WPF
        services.AddRouteableHost(Runtime.Wpf, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
#elif AVALONIA
        services.AddRouteableHost(Runtime.Avalonia, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
#elif WINUI3
        services.AddRouteableHost(Runtime.WinUI3, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
#else
        services.AddRouteableHost(Runtime.Uno, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
#endif
        services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        services.AddSingleton<IRouteableDispatcher, RouteableDispatcher>();
        services.AddSingleton<IRouteableViewBinder, RouteableViewBinder>();

        configure?.Invoke(services);
        // Add the routes the generator found, unless the application registered them (or called AddGeneratedRoutes()).
        global::Routeables.Abstraction.Registrations.RouteableAutoRegistration.Apply(services);
        var provider = services.BuildRouteableProvider();
        ServiceLocator.Services = provider;
        afterBuild?.Invoke(provider);
        return provider;
    }

    /// <summary>
    /// Registers the dialog, content dialog, task dialog and notification services of the host, and the <see cref="IMessageBoxService"/> that draws them.
    /// A second call replaces the first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="messageBoxService">Builds the <see cref="IMessageBoxService"/>; <see cref="DefaultMessageBoxService"/> when not set.</param>
    /// <param name="notificationService">Builds the <see cref="IRouteableNotificationService"/>; Routeables' own when not set.</param>
    /// <param name="icon">The icon of the dialog windows drawn by <see cref="DefaultMessageBoxService"/> (WPF and Avalonia).</param>
    public static IServiceCollection AddServices(
        IServiceCollection services,
        Func<IServiceProvider, IMessageBoxService>? messageBoxService,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService,
        string? icon)
    {
        ArgumentNullException.ThrowIfNull(services);

#if !WPF && !AVALONIA
        // The platform dialogs follow the activity in front (Android, Uno): start this before the first one.
        Routeables.Native.NativeDialogs.Initialize();
#endif

        services.RemoveAll<IRouteableNotificationService>();
        services.RemoveAll<IRouteableDialogService>();
        services.RemoveAll<IMessageBoxService>();

#if WPF || AVALONIA
        // The notification service is built for each use; the host's one has nothing to keep.
        services.AddTransient(notificationService ?? (sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>())));
        services.AddSingleton<IMessageBoxService>(messageBoxService ?? (_ => new DefaultMessageBoxService(icon)));
#else
        services.AddSingleton<IMessageBoxService>(messageBoxService ?? (_ => new DefaultMessageBoxService()));
        services.AddSingleton(notificationService ?? (sp => new RouteableNotificationService(sp.GetRequiredService<IMessageBoxService>())));
#endif
        services.AddSingleton<IRouteableDialogService, RouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));
        return services;
    }

    /// <summary>The last window takes the container with it: the locator forgets it, then it is disposed.</summary>
    /// <param name="provider">The container of the host.</param>
    public static void Release(IServiceProvider provider)
    {
        ServiceLocator.Release(provider);

        if (provider is IDisposable disposable)
            disposable.Dispose();
    }
}
