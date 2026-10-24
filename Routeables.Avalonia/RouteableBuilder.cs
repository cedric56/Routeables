using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Services;

namespace Routeables.Avalonia;

/// <summary>
/// Entry points for bootstrapping an Avalonia application together with its dependency injection
/// container and the Routeables core (routing, injection, theming).
/// </summary>
public static class RouteableBuilder
{
    /// <summary>
    /// Adds Routeables to an <see cref="AppBuilder"/> the usual Avalonia way: the application keeps its own constructor
    /// (<c>AppBuilder.Configure&lt;App&gt;()</c>), and this builds the container with the router, the property injection and the
    /// theme service, then attaches it. An <see cref="Application"/> that needs the container reads it with <see cref="GetRouteableServices"/>.
    /// The dialogs, content dialogs, task dialogs and notifications are optional: register them in
    /// <paramref name="configureExternalServices"/> with <c>UseRouteablesServices(icon)</c> from <c>Routeables.Avalonia.Services</c>.
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;()
    ///     .UseRouteables(services =>
    ///     {
    ///         services.UseRouteablesServices("avares://MyApp/Assets/icon.ico");
    ///         services.AddGeneratedRoutes();
    ///     })
    ///     .UsePlatformDetect();
    /// </code>
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configureExternalServices">Optional callback to register additional services with the container.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <param name="onDispose">Optional callback executed when the application window closes and disposes.</param>
    /// <returns>The same <see cref="AppBuilder"/>, ready for further setup.</returns>
    public static AppBuilder UseRouteables(
        this AppBuilder builder,
        Action<IServiceCollection>? configureExternalServices = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var provider = BuildProvider(configureExternalServices, afterBuild);
        return Attach(builder, provider, onDispose);
    }

    /// <summary>
    /// The container of the running Routeables host, for an <see cref="Application"/> built with
    /// <see cref="UseRouteables"/> that has no constructor parameter to receive it.
    /// </summary>
    /// <exception cref="InvalidOperationException">No Routeables host is running.</exception>
    public static IServiceProvider GetRouteableServices(this Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ServiceLocator.Services
            ?? throw new InvalidOperationException("No Routeables host is running. Add it with AppBuilder.UseRouteables(...).");
    }

    /// <summary>
    /// Shows the main view of the application: the <see cref="Window"/> on a desktop, the view on a single-view platform
    /// (mobile, browser). The view gets the main view model as its <c>DataContext</c> unless it already has one.
    /// Call it from <c>OnFrameworkInitializationCompleted</c>, before <c>base.OnFrameworkInitializationCompleted()</c>.
    /// <code>
    /// this.OpenMainView(ApplicationLifetime is ISingleViewApplicationLifetime ? new MainView() : new MainWindow());
    /// </code>
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="view">A <see cref="Window"/> for a desktop lifetime; any control for a single-view lifetime.</param>
    /// <exception cref="InvalidOperationException">No Routeables host is running, or the lifetime cannot show this view.</exception>
    public static void OpenMainView(this Application application, Control view)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(view);

        var provider = application.GetRouteableServices();
        view.DataContext ??= provider.GetService<IMainRouteables>();

        switch (application.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop when view is Window window:
                desktop.MainWindow = window;
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = view;
                break;
            default:
                throw new InvalidOperationException(
                    "This application lifetime cannot show " + view.GetType().Name
                    + ": a desktop needs a Window, a single-view platform needs a control.");
        }
    }

    /// <summary>
    /// Configures an <see cref="AppBuilder"/> for an application-derived type,
    /// constructing the app instance from the built service provider. Prefer <see cref="UseRouteables"/> when the application
    /// does not need the provider in its constructor.
    /// </summary>
    /// <typeparam name="TApp">The application type to configure.</typeparam>
    /// <param name="appFactory">A factory that creates the application instance given the built service provider.</param>
    /// <param name="configureExternalServices">Optional callback to register additional services with the container.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <param name="onDispose">Optional callback executed when the application window closes and disposes.</param>
    /// <returns>The configured <see cref="AppBuilder"/>, ready for further setup.</returns>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static AppBuilder CreateRouteableApp<TApp>(
       Func<IServiceProvider, TApp> appFactory,
       Action<IServiceCollection>? configureExternalServices = null,
       Action<IServiceProvider>? afterBuild = null,
       Action? onDispose = null) where TApp : Application
    {
        var provider = BuildProvider(configureExternalServices, afterBuild);
        return Attach(AppBuilder.Configure(() => appFactory.Invoke(provider)), provider, onDispose);
    }

    private static IServiceProvider BuildProvider(
        Action<IServiceCollection>? configureExternalServices,
        Action<IServiceProvider>? afterBuild)
    {
        var services = new ServiceCollection();

        services.AddRouteableHost(Runtime.Avalonia, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
        services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();

        configureExternalServices?.Invoke(services);
        var provider = services.BuildRouteableProvider();
        ServiceLocator.Services = provider;
        afterBuild?.Invoke(provider);
        return provider;
    }

    private static AppBuilder Attach(AppBuilder appBuilder, IServiceProvider provider, Action? onDispose)
    {
        return appBuilder
            .AfterSetup(builder =>
            {
                var app = builder.Instance;
                if (app != null)
                {
                    app.Styles.Add(new ErrorStyle());

                    TopLevelStateManager.Initialize();
                    TopLevelStateManager.Default.ActiveChanged += OnActived;

                    void OnActived(object? sender, EventArgs e)
                    {
                        TopLevelStateManager.Default.ActiveChanged -= OnActived;

                        var topLevel = TopLevelStateManager.Default.GetActive(false);

                        // The main view model is a singleton (RegisterMainRouteables refuses a transient one), so this is the
                        // instance the window displays.
                        try { provider.GetService<IMainRouteables>()?.SelectFirst(); }
                        catch (Exception ex) { RouteableErrors.Report(provider, "RouteableBuilder.SelectFirst", ex); }   // raised from an event: it must not end the application

                        topLevel?.Closed += OnClosed;

                        void OnClosed(object? sender, EventArgs e)
                        {
                            topLevel?.Closed -= OnClosed;

                            try { onDispose?.Invoke(); }
                            catch (Exception ex) { RouteableErrors.Report(provider, "RouteableBuilder.OnDispose", ex); }   // the provider is still disposed below

                            // The static locator must not keep serving the service of a host that is gone (a second host
                            // in the same process, or the next test).
                            if (ReferenceEquals(ServiceLocator.Services, provider))
                                ServiceLocator.Services = null;

                            if (provider is IDisposable disposable)
                                disposable.Dispose();
                        }
                    }
                }
            });
    }
}