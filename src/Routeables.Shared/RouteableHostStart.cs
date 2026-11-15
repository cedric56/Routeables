using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Services;

// One file for every host: Routeables.Wpf, Routeables.Avalonia and Routeables.Maui link it (and define WPF, AVALONIA or MAUI),
// Routeables.Uno and Routeables.WinUI3 link it too. What the extensions of the hosts do the same way when the application starts and when it ends.
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
        // The routes the generator found are added unless the application registered them (or called AddGeneratedRoutes()).
        global::Routeables.Abstraction.Registrations.RouteableAutoRegistration.Apply(services);
        var provider = services.BuildRouteableProvider();
        ServiceLocator.Services = provider;
        afterBuild?.Invoke(provider);
        return provider;
    }

    /// <summary>
    /// What a window does the first time it is activated: selects the first entry of the menu view model. It is a singleton
    /// (<c>RegisterMenuViewModel</c> refuses a transient one), so this is the instance the window displays.
    /// </summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="source">The type reported when it fails.</param>
    public static void SelectFirst(IServiceProvider provider, Type source)
    {
        // Raised from an event: it must not end the application.
        try { provider.GetService<IRouteableMenuViewModel>()?.SelectFirst(); }
        catch (Exception ex) { RouteableErrors.Report(provider, ex, source); }
    }

    /// <summary>Runs what the application asked for when its window closes; the container is disposed after it, so a failure is reported and goes no further.</summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="onDispose">The callback of the application, or <see langword="null"/>.</param>
    /// <param name="source">The type reported when it fails.</param>
    public static void RunOnDispose(IServiceProvider provider, Action? onDispose, Type source)
    {
        try { onDispose?.Invoke(); }
        catch (Exception ex) { RouteableErrors.Report(provider, ex, source); }
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
