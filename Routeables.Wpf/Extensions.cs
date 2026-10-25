using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Wpf.Services;

namespace Routeables.Wpf;

/// <summary>Adds Routeables to a WPF application.</summary>
public static class Extensions
{
    /// <summary>
    /// Adds Routeables to the application: builds the container, registers <typeparamref name="TMain"/> as the main view
    /// model and the services of the host (dialogs, notifications, theme). Call it once, when the application starts.
    /// </summary>
    /// <typeparam name="TMain">The main view model: a singleton.</typeparam>
    /// <param name="application">The application.</param>
    /// <param name="configure">Registers the routes and the services of the application.</param>
    /// <param name="afterBuild">Called with the container once it is built.</param>
    /// <returns>The container.</returns>
    public static IServiceProvider UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this Application application,
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null)
        where TMain : class, IMainRouteables
    {
        ArgumentNullException.ThrowIfNull(application);
        return BuildProvider(services =>
        {
            services.RegisterMainRouteables<TMain>();
            services.UseRouteablesServices();
            configure?.Invoke(services);
        }, afterBuild);
    }

    /// <summary>
    /// Adds Routeables to the application and opens its main window, in one call. The container is built now; the window is
    /// opened as soon as the application runs, so the call may be made in the constructor of the application, in
    /// <c>OnStartup</c> or in a handler of <c>Startup</c>: it is the same (<see cref="OpenMainWindow"/> does the opening).
    /// </summary>
    /// <typeparam name="TMain">The main view model: a singleton.</typeparam>
    /// <param name="application">The application.</param>
    /// <param name="configure">Registers the routes and the services of the application.</param>
    /// <param name="view">Creates the main view; it gets the main view model as its data context when it has none.</param>
    /// <param name="configureWindow">Sets the window up (title, size, icon).</param>
    /// <param name="afterBuild">Called with the container once it is built.</param>
    /// <param name="onDispose">Called when the window is closed, before the container is disposed.</param>
    /// <returns>The container.</returns>
    public static IServiceProvider UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this Application application,
        Action<IServiceCollection> configure,
        Func<FrameworkElement> view,
        Action<Window>? configureWindow = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null)
        where TMain : class, IMainRouteables
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(view);

        var provider = application.UseRouteables<TMain>(configure, afterBuild);

        // Queued: it runs once the application's message loop does, after Startup, wherever this call was made.
        application.Dispatcher.BeginInvoke(() => application.OpenMainWindow(view(), configureWindow, onDispose));
        return provider;
    }

    /// <summary>Gets the container built by <see cref="UseRouteables{TMain}"/>.</summary>
    /// <exception cref="InvalidOperationException">Routeables was not added to the application.</exception>
    public static IServiceProvider GetRouteableServices(this Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ServiceLocator.Services
            ?? throw new InvalidOperationException("No Routeables host is running. Add it with Application.UseRouteables(...).");
    }

    /// <summary>
    /// Opens the main window around <paramref name="content"/>: its data context is the main view model, the first entry of
    /// the menu is selected when the window is first activated, and the container is disposed when the window is closed.
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="content">The main view (a <see cref="RouteableFor"/> for instance).</param>
    /// <param name="configure">Sets the window up (title, size, icon).</param>
    /// <param name="onDispose">Called when the window is closed, before the container is disposed.</param>
    /// <returns>The window.</returns>
    public static Window OpenMainWindow(
        this Application application,
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
        application.MainWindow ??= window;
        window.Show();
        return window;
    }

    /// <summary>
    /// Attaches a window you created yourself: dialogs and notifications are shown over it, and the first entry of the menu
    /// is selected when it is first activated. The last attached window to close disposes the container.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="onDispose">Called when the window is closed, before the container is disposed.</param>
    /// <returns><paramref name="window"/>.</returns>
    public static Window AttachRouteables(this Window window, Action? onDispose = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        var provider = ServiceLocator.Services
            ?? throw new InvalidOperationException("No Routeables host is running. Call Application.UseRouteables(...) before attaching a window.");

        RouteableWindows.Add(window);

        var selected = false;
        void OnActivated(object? sender, EventArgs e)
        {
            RouteableWindows.Activated(window);
            if (selected)
                return;

            selected = true;

            // The main view model is a singleton (RegisterMainRouteables refuses a transient one), so this is the
            // instance the window displays.
            try { provider.GetService<IMainRouteables>()?.SelectFirst(); }
            catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(Extensions)); }   // raised from an event: it must not end the application
        }

        void OnClosed(object? sender, EventArgs e)
        {
            window.Activated -= OnActivated;
            window.Closed -= OnClosed;
            RouteableWindows.Remove(window);

            try { onDispose?.Invoke(); }
            catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(Extensions)); }   // the provider is still disposed below

            // The last window takes the container with it. The static locator must not keep serving the service of a host
            // that is gone (a second host in the same process).
            if (RouteableWindows.Active is null)
            {
                if (ReferenceEquals(ServiceLocator.Services, provider))
                    ServiceLocator.Services = null;

                if (provider is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        // The Back request of the window: Alt+Left, the back key of a keyboard and the back button of a mouse (the NavigationCommands.BrowseBack
        // command) go back one page of the router when it has one.
        var back = new CommandBinding(
            NavigationCommands.BrowseBack,
            (_, e) => e.Handled = RouteableBack.TryGoBack(provider),
            (_, e) =>
            {
                e.CanExecute = RouteableBack.CanGoBack(provider);
                e.Handled = true;
            });
        window.CommandBindings.Add(back);

        window.Activated += OnActivated;
        window.Closed += OnClosed;
        window.Closed += (_, _) => window.CommandBindings.Remove(back);
        return window;
    }

    private static IServiceProvider BuildProvider(
        Action<IServiceCollection> configure,
        Action<IServiceProvider>? afterBuild)
    {
        var services = new ServiceCollection();

        services.AddRouteableHost(Runtime.Wpf, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
        services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        services.AddSingleton<IRouteableDispatcher, RouteableDispatcher>();

        configure(services);
        var provider = services.BuildRouteableProvider();
        ServiceLocator.Services = provider;
        afterBuild?.Invoke(provider);
        return provider;
    }
}
