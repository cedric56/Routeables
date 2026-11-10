using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Injections;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Blazor.Services;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Services;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Blazor;

/// <summary>
/// Registers Routeables in a Blazor application's <see cref="IServiceCollection"/>.
/// </summary>
public static class BlazorRouteablesExtensions
{
    /// <summary>
    /// <c>UseRouteablesServer&lt;TMain&gt;</c> for an application that has no menu: no main view model to write. The first page is the
    /// one the <c>RouteableRegion</c> of the layout opens (<c>&lt;RouteableRegion Route="/hello/World" /&gt;</c>) when the address does not choose one.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="onCircuitProviderChanged">Called with the provider of a circuit that takes over, as for <c>UseRouteablesServer&lt;TMain&gt;</c>.</param>
    public static IServiceCollection UseRouteablesServer(this IServiceCollection services,
        Action<IServiceCollection>? configure = null,
        Func<IServiceProvider, Task>? onCircuitProviderChanged = null)
        => services.UseRouteablesServer<DefaultMainViewModel>(configure, onCircuitProviderChanged);

    /// <summary>
    /// Starts Routeables in a Blazor Server application: registers the router, the property injection, the address synchronization,
    /// the environment information and the theme, and <typeparamref name="TMain"/> as the main view model (the menu), scoped: one per circuit.
    /// It registers the dialogs (message boxes, content dialogs and task dialogs), modals and notifications too (<c>UseRouteablesServices()</c> of
    /// <c>Routeables.Blazor.Services</c>); call that one again only to replace a service or to move the notifications.
    /// <code>
    /// services.UseRouteablesServer&lt;MainViewModel&gt;();
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="onCircuitProviderChanged">Optional callback executed when the circuit provider initializes.</param>
    public static IServiceCollection UseRouteablesServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Action<IServiceCollection>? configure = null,
        Func<IServiceProvider, Task>? onCircuitProviderChanged = null)
        where TMain : class, IMainViewModel
        => services.UseRouteablesCore<TMain>(Runtime.BlazorServer, configure, onCircuitProviderChanged);

    /// <summary>
    /// <c>UseRouteablesWebAssembly&lt;TMain&gt;</c> for an application that has no menu: no main view model to write.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    public static IServiceCollection UseRouteablesWebAssembly(this IServiceCollection services, Action<IServiceCollection>? configure = null)
        => services.UseRouteablesWebAssembly<DefaultMainViewModel>(configure);

    /// <summary>
    /// Starts Routeables in a Blazor WebAssembly application: the same as <see cref="UseRouteablesServer{TMain}"/>, for the browser.
    /// <code>
    /// builder.Services.UseRouteablesWebAssembly&lt;MainViewModel&gt;();
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    public static IServiceCollection UseRouteablesWebAssembly<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
        => services.UseRouteablesCore<TMain>(Runtime.BlazorWebAssembly, configure, null);

    /// <summary>
    /// <c>UseRouteablesHybrid&lt;TMain&gt;</c> for an application that has no menu: no main view model to write.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    public static IServiceCollection UseRouteablesHybrid(this IServiceCollection services, Action<IServiceCollection>? configure = null)
        => services.UseRouteablesHybrid<DefaultMainViewModel>(configure);

    /// <summary>
    /// Starts Routeables in a MAUI Blazor Hybrid application: the same as <see cref="UseRouteablesServer{TMain}"/>, in a web view.
    /// <code>
    /// builder.Services.UseRouteablesHybrid&lt;MainViewModel&gt;();
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    public static IServiceCollection UseRouteablesHybrid<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
        => services.UseRouteablesCore<TMain>(Runtime.MauiHybrid, configure, null);

    private static IServiceCollection UseRouteablesCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Runtime runtime,
        Action<IServiceCollection>? configure,
        Func<IServiceProvider, Task>? onCircuitProviderChanged)
        where TMain : class, IMainViewModel
    {
        ArgumentNullException.ThrowIfNull(services);
        if (runtime == Runtime.BlazorServer
            && services.Any(d => d.ServiceType == typeof(IMainViewModel) && d.Lifetime != ServiceLifetime.Scoped))
            throw new InvalidOperationException(
                "RegisterMainViewModel ran before UseRouteablesServer and registered the main view model as a singleton, which Blazor Server shares between circuits. "
                + "Remove it: UseRouteablesServer<TMain>() registers the main view model.");
        services.TryAddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));

        services.AddScoped<RouteableUrlSync>();
        // One activator for the circuit: Blazor asks it for the components it renders, and the views of the router get it injected.
        services.AddScoped<RouteableComponentActivator>();
        services.TryAddScoped<Microsoft.AspNetCore.Components.IComponentActivator>(sp => sp.GetRequiredService<RouteableComponentActivator>());
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());

        services.AddSingleton<IRouteableInjection>(
            new RouteablePropertyInjection(
                new RouteablePropertyEntry(typeof(InjectAttribute)),
                new RouteablePropertyEntry(typeof(ParameterAttribute), FromContainer: false)));

        services.AddSingleton<Func<IServiceProvider, Task>>(onCircuitProviderChanged ?? (_ => Task.CompletedTask));
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.RegisterEnvironments(runtime);
        services.AddScoped<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        services.AddScoped<IRouteableDispatcher, RouteableDispatcher>();
        services.RegisterMainViewModel<TMain>();
        services.UseRouteablesServices();
        configure?.Invoke(services);
        // The routes the generator found are added unless the application registered them (or called AddGeneratedRoutes()).
        global::Routeables.Abstraction.Registrations.RouteableAutoRegistration.Apply(services);
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IRouteableDialogService"/> (message boxes, content dialogs and task dialogs), modal and notification
    /// services, and the <see cref="IMessageBoxService"/> that draws the dialogs; <c>UseRouteablesServer</c> already calls it,
    /// and a second call replaces the first. Display them with the hosts
    /// of <c>MainLayout</c>, or place <c>RouteableDialogHost</c>, <c>RouteableContentDialogHost</c>, <c>RouteableTaskDialogHost</c>,
    /// <c>RouteableModalHost</c> and <c>RouteableNotificationHost</c> once in your own layout.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="messageBoxService">Builds the <see cref="IMessageBoxService"/> that draws the dialogs; <see cref="DefaultMessageBoxService"/> (plain Razor, shown by the hosts of the layout) when not set. Give another one to draw them with a UI library, such as the MudBlazor one of the Reshapes.</param>
    /// <param name="notificationService">Factory of the <see cref="IRouteableNotificationService"/>; null keeps Routeables' own.</param>
    /// <param name="position">The screen position where toast notifications appear, bottom-left by default.</param>
    /// <param name="maxDispayments">The maximum number of notifications displayed at once, 5 by default.</param>
    public static IServiceCollection UseRouteablesServices(this IServiceCollection services,
        Func<IServiceProvider, IMessageBoxService>? messageBoxService = null,
        Func<IServiceProvider, IRouteableNotificationService>? notificationService = null,
        RouteableNotificationPosition position = RouteableNotificationPosition.BottomLeft,
        int maxDispayments = 5)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<RouteableModalService>();
        services.RemoveAll<RouteableNotificationOptions>();
        services.AddScoped<RouteableModalService>();

        services.RemoveAll<RouteableMessageQueue>();
        services.RemoveAll<RouteableContentDialogQueue>();
        services.RemoveAll<RouteableTaskDialogQueue>();
        services.AddScoped<RouteableMessageQueue>();
        services.AddScoped<RouteableContentDialogQueue>();
        services.AddScoped<RouteableTaskDialogQueue>();

        // The message box service draws the dialogs; one dialog service asks it, whatever draws them.
        services.RemoveAll<DefaultMessageBoxService>();
        services.RemoveAll<IMessageBoxService>();
        services.RemoveAll<IRouteableDialogService>();
        services.AddScoped(sp => new DefaultMessageBoxService(
            sp.GetRequiredService<RouteableMessageQueue>(),
            sp.GetRequiredService<RouteableContentDialogQueue>(),
            sp.GetRequiredService<RouteableTaskDialogQueue>()));
        services.AddScoped<IMessageBoxService>(messageBoxService ?? (sp => sp.GetRequiredService<DefaultMessageBoxService>()));
        services.AddScoped<IRouteableDialogService>(sp => new RouteableDialogService(sp.GetRequiredService<IMessageBoxService>()));

        services.RemoveAll<RouteableSystemNotifications>();
        services.AddScoped(sp => new RouteableSystemNotifications(sp.GetService<IRouteableSystemNotificationSource>()));
        services.AddSingleton(new RouteableNotificationOptions
        {
            Position = position,
            MaxDisplayed = maxDispayments
        });
        services.RegisterServices<IRouteableNotificationService, RouteableNotificationService>(
            sp => new RouteableNotificationService(sp.GetRequiredService<RouteableNotificationOptions>(), sp.GetService<IRouteableSystemNotificationSource>()),
            notificationService);
        return services;
    }

    /// <summary>
    /// Turns on the system notifications of <see cref="IRouteableNotificationService"/> (<c>CreateNotificationAsync</c>, <c>ActiveNotifications</c>,
    /// <c>CloseAll</c>, <c>NotificationCompleted</c>, <c>InitializeAsync</c>): the notification center of the browser, through its Notification API.
    /// It is the counterpart of <c>WithAppNotifications</c> of Avalonia.Labs.Notifications, and separate from <c>UseRouteables*</c>: without this
    /// call the service creates none. Call it once at startup, before or after <see cref="UseRouteablesServices"/>.
    /// The user must grant the permission: <c>CreateNotificationAsync</c> asks for it when it was not asked yet (from a click) and returns
    /// <see langword="null"/> when it is denied, when the browser has no Notification API, or while the page is prerendered.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="options">The <see cref="AppNotificationOptions"/>; the defaults when <see langword="null"/>.</param>
    public static IServiceCollection WithAppNotifications(this IServiceCollection services, AppNotificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        options ??= new AppNotificationOptions();
        services.RemoveAll<AppNotificationOptions>();
        services.RemoveAll<BrowserSystemNotifications>();
        services.RemoveAll<IRouteableSystemNotificationSource>();
        services.AddSingleton(options);

        // MAUI Blazor Hybrid runs in the web view of the platform, which has no usable Notification API: the notification center
        // of the platform (Routeables.Native) takes its place. Server and WebAssembly keep the one of the browser.
        if (Routeables.Native.SystemNotificationCenter.IsSupported)
        {
            Routeables.Native.SystemNotificationCenter.Current.Configure(new Routeables.Native.AppNotificationOptions
            {
                ClearOnAppClose = options.ClearOnAppClose,
                ChannelName = options.ChannelName
            });
            services.AddSingleton<IRouteableSystemNotificationSource>(Routeables.Native.SystemNotificationCenter.Current);
            return services;
        }

        services.AddScoped(sp => new BrowserSystemNotifications(
            sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(), sp.GetRequiredService<AppNotificationOptions>()));
        services.AddScoped<IRouteableSystemNotificationSource>(sp => sp.GetRequiredService<BrowserSystemNotifications>());
        return services;
    }

    private static void RegisterServices<TInterface, TImplementation>(this IServiceCollection services,
        Func<IServiceProvider, TImplementation> implementationFactory,
        Func<IServiceProvider, TInterface>? factory = null)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.RemoveAll<TInterface>();
        services.RemoveAll<TImplementation>();
        Func<IServiceProvider, TInterface>? service = factory;
        if (service == null)
        {
            service = sp => implementationFactory(sp);
            services.AddScoped(sp => (TImplementation)sp.GetRequiredService<TInterface>());
        }
        services.AddScoped(service);
    }
}
