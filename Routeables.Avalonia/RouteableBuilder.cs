using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Avalonia.Services;
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
    /// (<c>AppBuilder.Configure&lt;App&gt;()</c>), and this builds the container with the router, the property injection, the
    /// theme service and <typeparamref name="TMain"/> as the main view model (the menu), then attaches it. An <see cref="Application"/>
    /// that needs the container reads it with <see cref="GetRouteableServices"/>.
    /// It registers the dialogs, content dialogs, task dialogs and notifications too; the dialog windows take the icon of the window that opens them.
    /// To draw them another way, register your own <c>IMessageBoxService</c> in <paramref name="configure"/>.
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;()
    ///     .UseRouteables&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes())
    ///     .UsePlatformDetect();
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <param name="onDispose">Optional callback executed when the application window closes and disposes.</param>
    /// <param name="view">
    /// Optional: creates the main view from the lifetime of the application, so the application has no
    /// <c>OnFrameworkInitializationCompleted</c> to write: <c>lifetime =&gt; lifetime is IClassicDesktopStyleApplicationLifetime ? new MainWindow() : new MainView()</c>.
    /// The view is shown as <see cref="OpenMainView"/> does, as soon as the lifetime is known: during the setup when it already is,
    /// otherwise once the application runs.
    /// </param>
    /// <returns>The same <see cref="AppBuilder"/>, ready for further setup.</returns>
    public static AppBuilder UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this AppBuilder builder,
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null,
        Func<IApplicationLifetime?, Control>? view = null)
        where TMain : class, IMainRouteables
    {
        ArgumentNullException.ThrowIfNull(builder);

        var provider = BuildProvider(services =>
        {
            services.RegisterMainRouteables<TMain>();
            services.UseRouteablesServices();
            configure?.Invoke(services);
        }, afterBuild);
        return Attach(builder, provider, onDispose, view);
    }

    /// <summary>
    /// The container of the running Routeables host, for an <see cref="Application"/> built with
    /// <see cref="UseRouteables{TMain}"/> that has no constructor parameter to receive it.
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
    /// (browser) or in the activity of an Android application. The view gets the main view model as its <c>DataContext</c> unless it already has one.
    /// The Back request of the system (the button or gesture of Android, the mouse back button and Alt+Left of a desktop) goes back one page of the router when it has one, see <see cref="RouteableBack"/>.
    /// Call it from <c>OnFrameworkInitializationCompleted</c>, before <c>base.OnFrameworkInitializationCompleted()</c>.
    /// <code>
    /// this.OpenMainView(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime ? new MainWindow() : new MainView());
    /// </code>
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="view">A <see cref="Window"/> for a desktop lifetime; any control for the other lifetimes.</param>
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
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => view;
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = view;
                break;
            default:
                throw new InvalidOperationException(
                    "This application lifetime cannot show " + view.GetType().Name
                    + ": a desktop needs a Window, a single-view platform needs a control.");
        }

        // The Back request of the system goes back one page of the router.
        BackRequests.Attach(view, provider);
    }

    private static IServiceProvider BuildProvider(
        Action<IServiceCollection>? configure,
        Action<IServiceProvider>? afterBuild)
    {
        var services = new ServiceCollection();

        services.AddRouteableHost(Runtime.Avalonia, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));
        services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        services.AddSingleton<IRouteableDispatcher, RouteableDispatcher>();

        configure?.Invoke(services);
        var provider = services.BuildRouteableProvider();
        ServiceLocator.Services = provider;
        afterBuild?.Invoke(provider);
        return provider;
    }

    // The lifetime is set by the code that starts the application: when it is already there the view is shown now, which is
    // before OnFrameworkInitializationCompleted; when it is not, the view is shown as soon as the application runs.
    private static void OpenWhenKnown(Application app, Func<IApplicationLifetime?, Control> view)
    {
        void Open() => app.OpenMainView(view(app.ApplicationLifetime));

        if (app.ApplicationLifetime is not null)
            Open();
        else
            global::Avalonia.Threading.Dispatcher.UIThread.Post(Open);
    }

    private static AppBuilder Attach(AppBuilder appBuilder, IServiceProvider provider, Action? onDispose, Func<IApplicationLifetime?, Control>? view)
    {
        return appBuilder
            .AfterSetup(builder =>
            {
                var app = builder.Instance;
                if (app != null)
                {
                    app.Styles.Add(new ErrorStyle());

                    if (view is not null)
                        OpenWhenKnown(app, view);

                    TopLevelStateManager.Initialize();
                    TopLevelStateManager.Default.ActiveChanged += OnActived;

                    void OnActived(object? sender, EventArgs e)
                    {
                        TopLevelStateManager.Default.ActiveChanged -= OnActived;

                        var topLevel = TopLevelStateManager.Default.GetActive(false);

                        // The main view model is a singleton (RegisterMainRouteables refuses a transient one), so this is the
                        // instance the window displays.
                        try { provider.GetService<IMainRouteables>()?.SelectFirst(); }
                        catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(RouteableBuilder)); }   // raised from an event: it must not end the application

                        topLevel?.Closed += OnClosed;

                        void OnClosed(object? sender, EventArgs e)
                        {
                            topLevel?.Closed -= OnClosed;

                            try { onDispose?.Invoke(); }
                            catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(RouteableBuilder)); }   // the provider is still disposed below

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