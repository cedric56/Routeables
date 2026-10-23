using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Uno;

/// <summary>
/// Entry points for bootstrapping an Uno Platform application together with its dependency injection container and the
/// Routeables core (routing, injection, theming).
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Builds the container with the router, the property injection and the theme service, and keeps it for the views.
    /// Call it once, from the constructor of the <see cref="Microsoft.UI.Xaml.Application"/>: the container is then ready when
    /// <c>OnLaunched</c> creates the window. The dialogs, content dialogs, task dialogs and notifications are optional: register
    /// them in <paramref name="configureExternalServices"/> with <c>UseRouteablesServices()</c> from <c>Routeables.Uno.Services</c>.
    /// <code>
    /// Services = this.UseRouteables(services =>
    /// {
    ///     services.UseRouteablesServices();
    ///     services.AddGeneratedRoutes();
    ///     services.RegisterMainRouteables&lt;MainViewModel&gt;(ServiceLifetime.Singleton);
    /// });
    /// </code>
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="configureExternalServices">Optional callback to register additional services with the container.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <returns>The container, which <see cref="GetRouteableServices"/> returns too.</returns>
    public static IServiceProvider UseRouteables(
        this Microsoft.UI.Xaml.Application application,
        Action<IServiceCollection>? configureExternalServices = null,
        Action<IServiceProvider>? afterBuild = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        return BuildProvider(configureExternalServices, afterBuild);
    }

    /// <summary>The container of the running Routeables host.</summary>
    /// <exception cref="InvalidOperationException">No Routeables host is running.</exception>
    public static IServiceProvider GetRouteableServices(this Microsoft.UI.Xaml.Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ServiceLocator.Services
            ?? throw new InvalidOperationException("No Routeables host is running. Add it with Application.UseRouteables(...).");
    }

    /// <summary>
    /// Opens the main window of the application: creates it with <paramref name="content"/>, gives the content the main view model
    /// as its <c>DataContext</c> (unless it already has one), connects it to Routeables (see <see cref="AttachRouteables"/>) and
    /// activates it. Call it from <c>OnLaunched</c>.
    /// <code>
    /// protected override void OnLaunched(LaunchActivatedEventArgs args) =>
    ///     _window = this.OpenMainWindow(new MainPage());
    /// </code>
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="content">The root of the window, usually the page that shows the menu and the current view.</param>
    /// <param name="configure">Optional, to set the title, the size or anything else before the window is activated.</param>
    /// <param name="onDispose">Optional callback executed when the window closes.</param>
    /// <returns>The window.</returns>
    /// <exception cref="InvalidOperationException">No Routeables host is running.</exception>
    public static Window OpenMainWindow(
        this Microsoft.UI.Xaml.Application application,
        FrameworkElement content,
        Action<Window>? configure = null,
        Action? onDispose = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(content);

        var provider = application.GetRouteableServices();
        if (content.DataContext is null)
            content.DataContext = provider.GetService<IMainRouteables>();

        var window = new Window { Content = content };
        configure?.Invoke(window);
        window.AttachRouteables(onDispose);
        window.Activate();
        return window;
    }

    /// <summary>
    /// Connects a window to Routeables. <see cref="OpenMainWindow"/> does it for the main window; call this one for the others. Call it once the window has its content, before <c>Activate()</c>.
    /// The first time the window is activated the first entry of the main view model is selected; the dialogs and notifications of
    /// <c>Routeables.Uno.Services</c> are shown in the window the user last worked in; the theme service changes the theme of its content;
    /// and when the window closes <paramref name="onDispose"/> runs and the container is disposed.
    /// </summary>
    /// <param name="window">The main window.</param>
    /// <param name="onDispose">Optional callback executed when the window closes.</param>
    /// <returns>The same window.</returns>
    public static Window AttachRouteables(this Window window, Action? onDispose = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        var provider = ServiceLocator.Services
            ?? throw new InvalidOperationException("No Routeables host is running. Call Application.UseRouteables(...) before attaching a window.");

        RouteableWindows.Add(window);

        var selected = false;
        void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
                return;

            RouteableWindows.Activated(window);
            if (selected)
                return;

            selected = true;

            // The main view model is a singleton (RegisterMainRouteables refuses a transient one), so this is the
            // instance the window displays.
            try { provider.GetService<IMainRouteables>()?.SelectFirst(); }
            catch (Exception ex) { RouteableErrors.Report(provider, "RouteableBuilder.SelectFirst", ex); }   // raised from an event: it must not end the application
        }

        void OnClosed(object sender, WindowEventArgs e)
        {
            window.Activated -= OnActivated;
            window.Closed -= OnClosed;
            RouteableWindows.Remove(window);

            try { onDispose?.Invoke(); }
            catch (Exception ex) { RouteableErrors.Report(provider, "RouteableBuilder.OnDispose", ex); }   // the provider is still disposed below

            // The last window takes the container with it. The static locator must not keep serving the service of a host
            // that is gone (a second host in the same process, or the next test).
            if (RouteableWindows.Active is null)
            {
                if (ReferenceEquals(ServiceLocator.Services, provider))
                    ServiceLocator.Services = null;

                if (provider is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        window.Activated += OnActivated;
        window.Closed += OnClosed;
        return window;
    }

    private static IServiceProvider BuildProvider(
        Action<IServiceCollection>? configureExternalServices,
        Action<IServiceProvider>? afterBuild)
    {
        var services = new ServiceCollection();

        services.AddRouteableHost(Runtime.Uno, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
        services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();

        configureExternalServices?.Invoke(services);
        var provider = services.BuildRouteableProvider();
        ServiceLocator.Services = provider;
        afterBuild?.Invoke(provider);
        return provider;
    }
}
