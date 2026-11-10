using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.UI.Xaml;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Services;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;




#if WINUI3
using Routeables.WinUI3.Services;
using HostExtensions = Routeables.WinUI3.WinUI3RouteablesExtensions;
#else
using Routeables.Uno.Services;
using HostExtensions = Routeables.Uno.UnoRouteablesExtensions;
#endif
using System.Diagnostics.CodeAnalysis;

#if WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

/// <summary>
/// Entry points for bootstrapping an Uno Platform application together with its dependency injection container and the
/// Routeables core (routing, injection, theming).
/// </summary>
#if WINUI3
public static class WinUI3RouteablesExtensions
#else
public static class UnoRouteablesExtensions
#endif
{
#if !WINUI3
    private static BackRequests? _back;
#endif

    /// <summary>
    /// <c>UseRouteables&lt;TMain&gt;</c> for an application that has no menu: no main view model to write. The first page is the one the
    /// <c>RouteableRegion</c> of the main view opens (<c>Route="/hello/World"</c>).
    /// <code>
    /// Services = this.UseRouteables();
    /// </code>
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <returns>The container, which <see cref="GetRouteableServices"/> returns too.</returns>
    public static IServiceProvider UseRouteables(
        this Microsoft.UI.Xaml.Application application,
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null)
        => application.UseRouteables<DefaultMainViewModel>(configure, afterBuild);

    /// <summary>
    /// Builds the container with the router, the property injection, the theme service and <typeparamref name="TMain"/> as the main view
    /// model (the menu), and keeps it for the views.
    /// Call it once, from the constructor of the <see cref="Microsoft.UI.Xaml.Application"/>: the container is then ready when
    /// <c>OnLaunched</c> creates the window. The dialogs, content dialogs, task dialogs and notifications are registered too;
    /// call <c>UseRouteablesServices(sp => new MyMessageBoxService())</c> in <paramref name="configure"/> only to draw them another way.
    /// <code>
    /// Services = this.UseRouteables&lt;MainViewModel&gt;();
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="application">The application.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <returns>The container, which <see cref="GetRouteableServices"/> returns too.</returns>
    public static IServiceProvider UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this Microsoft.UI.Xaml.Application application,
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null)
        where TMain : class, IMainViewModel
    {
        ArgumentNullException.ThrowIfNull(application);
        return StartRouteables<TMain>(configure, afterBuild);
    }

    /// <summary>
    /// <c>UseRouteables&lt;TMain&gt;</c> that opens the main window, for an application that has no menu: no main view model to write.
    /// The first page is the one the <c>RouteableRegion</c> of the main view opens (<c>Route="/hello/World"</c>).
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="configure">Your own registrations (routes, modules, services).</param>
    /// <param name="view">Creates the main view.</param>
    /// <param name="configureWindow">Sets the window up (title, size, icon).</param>
    /// <param name="afterBuild">Called with the container once it is built.</param>
    /// <param name="onDispose">Called when the window is closed, before the container is disposed.</param>
    /// <returns>The container.</returns>
    public static IServiceProvider UseRouteables(
        this Microsoft.UI.Xaml.Application application,
        Action<IServiceCollection> configure,
        Func<FrameworkElement> view,
        Action<Window>? configureWindow = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null)
        => application.UseRouteables<DefaultMainViewModel>(configure, view, configureWindow, afterBuild, onDispose);

    /// <summary>
    /// Adds Routeables to the application and opens its main window, in one call, so the application has no <c>OnLaunched</c> to write.
    /// The container is built now; the window is created by the dispatcher of the application as soon as it runs, which is right after
    /// the constructor returns. Call it from the constructor of the <see cref="Microsoft.UI.Xaml.Application"/>.
    /// <code>
    /// Services = this.UseRouteables&lt;MainViewModel&gt;(services => { }, () => new MainPage(), window => window.Title = "My app");
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="application">The application.</param>
    /// <param name="configure">Your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="view">Creates the root of the window, usually the page that shows the menu and the current view; it gets the main view model as its data context when it has none.</param>
    /// <param name="configureWindow">Optional, to set the title, the size or anything else before the window is activated.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <param name="onDispose">Optional callback executed when the window closes.</param>
    /// <returns>The container.</returns>
    /// <exception cref="InvalidOperationException">The application has no dispatcher yet: create the window in <c>OnLaunched</c> with <see cref="OpenMainWindow"/>.</exception>
    public static IServiceProvider UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this Microsoft.UI.Xaml.Application application,
        Action<IServiceCollection> configure,
        Func<FrameworkElement> view,
        Action<Window>? configureWindow = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null)
        where TMain : class, IMainViewModel
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(view);

        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "The application has no dispatcher yet, so the window cannot be opened for it. Create the window in OnLaunched with OpenMainWindow.");

        var provider = StartRouteables<TMain>(configure, afterBuild);
        queue.TryEnqueue(() => application.OpenMainWindow(view(), configureWindow, onDispose));
        return provider;
    }

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

#if WINUI3
    /// <summary>
    /// Turns on the system notifications of <see cref="IRouteableNotificationService"/> (<c>CreateNotificationAsync</c>, <c>ActiveNotifications</c>,
    /// <c>CloseAll</c>, <c>NotificationCompleted</c>, <c>InitializeAsync</c>): the notification center of Windows, through <c>AppNotificationManager</c>
    /// of the Windows App SDK. Separate from <c>UseRouteables</c>: without this call the service creates none. Windows reports a tap, not a dismissal.
    /// <code>
    /// Services = this.UseRouteables(services => services.WithAppNotifications());
    /// </code>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The options (<c>ClearOnAppClose</c>); the defaults when <see langword="null"/>.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection WithAppNotifications(this IServiceCollection services, Routeables.Native.AppNotificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        Routeables.Native.SystemNotificationCenter.Current.Configure(options ?? new Routeables.Native.AppNotificationOptions());
        return services;
    }

#endif
    // What UseRouteables does, without an Application: the tests of the contract of the hosts call it, as an Application cannot be created outside Uno.
    internal static IServiceProvider StartRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null)
        where TMain : class, IMainViewModel
        => BuildProvider(services =>
        {
            services.RegisterMainViewModel<TMain>();
            services.UseRouteablesServices();
            configure?.Invoke(services);
            // The routes the generator found are added unless the application registered them (or called AddGeneratedRoutes()).
            global::Routeables.Abstraction.Registrations.RouteableAutoRegistration.Apply(services);
        }, afterBuild);

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
            content.DataContext = provider.GetService<IMainViewModel>();

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

#if !WINUI3
        // One listener for the whole application, whatever the number of windows.
        _back ??= new BackRequests(provider);
#endif

        var selected = false;
        void OnActivated(object sender, WindowActivatedEventArgs e)
        {
#if WINUI3
            if (e.WindowActivationState == WindowActivationState.Deactivated)
#else
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
#endif
                return;

            RouteableWindows.Activated(window);
            if (selected)
                return;

            selected = true;

            // The main view model is a singleton (RegisterMainViewModel refuses a transient one), so this is the
            // instance the window displays.
            try { provider.GetService<IMainViewModel>()?.SelectFirst(); }
            catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(HostExtensions)); }   // raised from an event: it must not end the application
        }

        void OnClosed(object sender, WindowEventArgs e)
        {
            window.Activated -= OnActivated;
            window.Closed -= OnClosed;
            RouteableWindows.Remove(window);

            try { onDispose?.Invoke(); }
            catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(HostExtensions)); }   // the provider is still disposed below

            // The last window takes the container with it. The static locator must not keep serving the service of a host
            // that is gone (a second host in the same process, or the next test).
            if (RouteableWindows.Active is null)
            {
#if !WINUI3
                _back?.Dispose();
                _back = null;
#endif

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
        Action<IServiceCollection>? configure,
        Action<IServiceProvider>? afterBuild)
    {
        var services = new ServiceCollection();

#if WINUI3
        services.AddRouteableHost(Runtime.WinUI3,
#else
        services.AddRouteableHost(Runtime.Uno,
#endif
             new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
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
}