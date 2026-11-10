using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Routing;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction;

/// <summary>
/// Registration and resolution helpers for the Routeables routing system: route/view/view-model
/// registration on <see cref="IServiceCollection"/>, and view/view-model/modal resolution
/// on <see cref="IServiceProvider"/>.
/// </summary>
public static partial class Extensions
{
    /// <summary>Replaces how the arguments of a route change are created: <paramref name="factory"/> returns a new <see cref="IRouteableArgs"/> each time. Without it the arguments are a <c>RouteableArgs</c>.</summary>
    public static void RegisterRouteableArgsFactory(this IServiceCollection services, Func<IRouteableArgs> factory)
    {
        services.AddSingleton<IRouteableArgsFactory>(sp => new RouteableArgsFactory(factory));
    }

    /// <summary>
    /// Registers the menu view model of a host (the menu or tab bar), which is the <see cref="IRouteableMenuViewModel"/> the views,
    /// the lock coordinator and the start selection all refer to.
    /// </summary>
    /// <remarks>
    /// Leave <paramref name="lifetime"/> out: the host decides, <see cref="ServiceLifetime.Scoped"/> for Blazor (one per circuit)
    /// and <see cref="ServiceLifetime.Singleton"/> for Avalonia, MAUI and Uno. Call <c>UseRouteables</c> first, so the host has said
    /// which it is. A <see cref="ServiceLifetime.Transient"/> one is refused:
    /// each resolution would be another instance, so the one the window displays would not be the one that is selected,
    /// locked or subscribed.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The lifetime of the menu view model; the one of the host when omitted.</param>
    /// <exception cref="ArgumentException"><paramref name="lifetime"/> is <see cref="ServiceLifetime.Transient"/>.</exception>
    public static IServiceCollection RegisterMenuViewModel<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMenu>(
        this IServiceCollection services, ServiceLifetime? lifetime = null)
        where TMenu : class, IRouteableMenuViewModel
    {
        ArgumentNullException.ThrowIfNull(services);
        var chosen = lifetime ?? services.GetRouteableLifetime();
        if (chosen == ServiceLifetime.Transient)
            throw new ArgumentException(
                "The main routeables must be Scoped or Singleton: a Transient one gives each consumer another instance.", nameof(lifetime));

        // The container builds TMenu under a key, so that it can check its dependencies when it is built. TMenu and IRouteableMenuViewModel
        // both give that instance once the entries of the modules (AddMenuEntries) have been added to its menu: whichever of the two
        // a consumer asks for, the menu is the same.
        services.Add(new ServiceDescriptor(typeof(TMenu), MenuKey, typeof(TMenu), chosen));
        services.Add(new ServiceDescriptor(typeof(TMenu), sp => WithModuleEntries(sp.GetRequiredKeyedService<TMenu>(MenuKey), sp), chosen));
        services.Add(new ServiceDescriptor(typeof(IRouteableMenuViewModel), sp => sp.GetRequiredService<TMenu>(), chosen));

        // The service that loads modules while the application runs; one for the application, whatever the main's lifetime.
        services.TryAddSingleton<RouteableModules>(sp => new RouteableModules(sp));
        services.TryAddSingleton<IRouteableModules>(sp => sp.GetRequiredService<RouteableModules>());
        return services;
    }

    /// <summary>
    /// The lifetime that lives as long as the routers of the host: <see cref="ServiceLifetime.Scoped"/> for Blazor (one per circuit),
    /// <see cref="ServiceLifetime.Singleton"/> everywhere else. The host says which with <see cref="RouteableHostLifetime"/> when
    /// <c>UseRouteables</c> runs; before that, or without a host, it is Singleton.
    /// </summary>
    public static ServiceLifetime GetRouteableLifetime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(RouteableHostLifetime) && services[i].ImplementationInstance is RouteableHostLifetime host)
                return host.Lifetime;
        return ServiceLifetime.Singleton;
    }

    /// <summary>
    /// Adds entries to the menu of the menu view model, from a module: a class library that registers its own routes and shows its
    /// own pages in the menu of the application that uses it.
    /// <code>
    /// public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    ///     => services.AddGeneratedRoutes()
    ///                .AddMenuEntries(() => [new RouteableMenuEntry&lt;OrdersViewModel&gt;("Orders", route: "/orders", template: "/orders")]);
    /// </code>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="entries">
    /// Builds the entries. It runs once for each menu view model, so each one has entries of its own (a lock or a selection is not
    /// shared between the circuits of a Blazor Server application): return new entries, not ones kept in a field.
    /// </param>
    /// <param name="index">
    /// Where the entries go in the menu: <c>0</c> puts them first, <c>1</c> after the first entry, and so on. It counts the entries the menu has
    /// when the module is added to it (the ones of the menu view model and of the modules added before), and a value past the end, like the
    /// default <see langword="null"/>, puts them last. The entries of one module stay together, in the order they were built.
    /// </param>
    /// <remarks>
    /// By default the entries come after the ones of the menu view model, in the order the modules were added. This works with
    /// <see cref="RegisterMenuViewModel{TMenu}"/>, whether a consumer asks for the type given to <c>RegisterMenuViewModel</c> or for
    /// <see cref="IRouteableMenuViewModel"/>. Adding the same module twice adds its entries once (the same <paramref name="entries"/> function);
    /// a function that captures something different on each call counts as another module.
    /// </remarks>
    public static IServiceCollection AddMenuEntries(this IServiceCollection services, Func<IEnumerable<IRouteableMenuEntry>> entries, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(entries);

        // A module added twice (a host that calls it and a helper that calls it too) adds its entries once.
        if (services.Any(d => d.ServiceType == typeof(RouteableMenuEntries) && d.ImplementationInstance is RouteableMenuEntries known && known.Build.Equals(entries)))
            return services;

        services.AddSingleton(new RouteableMenuEntries(entries, index));
        return services;
    }

    // The key under which the container builds the menu view model; see RegisterMenuViewModel.
    private const string MenuKey = "Routeables.MenuViewModel";

    private static IRouteableMenuViewModel WithModuleEntries(IRouteableMenuViewModel menuViewModel, IServiceProvider provider)
    {
        foreach (var module in provider.GetServices<RouteableMenuEntries>())
            module.AddTo(menuViewModel);

        // The modules loaded while running: the ones already loaded add their entries now, the next ones when they are loaded.
        provider.GetService<RouteableModules>()?.Attach(menuViewModel);

        return menuViewModel;
    }

    /// <summary>
    /// Registers the factory that builds the <see cref="IRouteableLockCoordinator"/> of a menu view model (see
    /// <see cref="RouterExtensions.CreateLockCoordinator"/>). <c>AddLocking()</c> of <c>Routeables.Locking</c> calls it with its own
    /// coordinator and adds the broker it listens to; call it yourself only to supply another coordinator.
    /// </summary>
    /// <typeparam name="TLockCoordinator">The coordinator; it is built with the provider and the menu view model.</typeparam>
    public static void RegisterLockCoordinator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TLockCoordinator>(this IServiceCollection services)
        where TLockCoordinator : IRouteableLockCoordinator
    {
        // The factory captures the provider it is resolved from. Scoped, so that provider is the router's own scope.
        services.TryAdd(new ServiceDescriptor(
            typeof(Func<IRouteableMenuViewModel, IRouteableLockCoordinator>),
            sp => (Func<IRouteableMenuViewModel, IRouteableLockCoordinator>)(manager =>
                ActivatorUtilities.CreateInstance<TLockCoordinator>(sp, manager)),
            ServiceLifetime.Scoped));
    }

    /// <summary>
    /// Creates a <see cref="IRouteableLockCoordinator"/> bound to <paramref name="menuViewModel"/>
    /// </summary>
    /// <param name="provider">The provider the factory is resolved from.</param>
    /// <param name="menuViewModel">The MVVM manager the coordinator will work with.</param>
    /// <returns>A new <see cref="IRouteableLockCoordinator"/> instance (one per call).</returns>
    public static IRouteableLockCoordinator GetLockCoordinator(this IServiceProvider provider, IRouteableMenuViewModel menuViewModel)
    {
        var factory = provider.GetRequiredService<Func<IRouteableMenuViewModel, IRouteableLockCoordinator>>();
        return factory.Invoke(menuViewModel);
    }
}
