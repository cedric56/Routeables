using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
    /// Registers the main view model of a host (the menu or tab bar), which is the <see cref="IMainViewModel"/> the views,
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
    /// <param name="lifetime">The lifetime of the main view model; the one of the host when omitted.</param>
    /// <exception cref="ArgumentException"><paramref name="lifetime"/> is <see cref="ServiceLifetime.Transient"/>.</exception>
    public static IServiceCollection RegisterMainViewModel<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this IServiceCollection services, ServiceLifetime? lifetime = null)
        where TMain : class, IMainViewModel
    {
        ArgumentNullException.ThrowIfNull(services);
        var chosen = lifetime ?? services.GetRouteableLifetime();
        if (chosen == ServiceLifetime.Transient)
            throw new ArgumentException(
                "The main routeables must be Scoped or Singleton: a Transient one gives each consumer another instance.", nameof(lifetime));

        // The container builds TMain under a key, so that it can check its dependencies when it is built. TMain and IMainViewModel
        // both give that instance once the entries of the modules (AddMenuEntries) have been added to its menu: whichever of the two
        // a consumer asks for, the menu is the same.
        services.Add(new ServiceDescriptor(typeof(TMain), MainKey, typeof(TMain), chosen));
        services.Add(new ServiceDescriptor(typeof(TMain), sp => WithModuleEntries(sp.GetRequiredKeyedService<TMain>(MainKey), sp), chosen));
        services.Add(new ServiceDescriptor(typeof(IMainViewModel), sp => sp.GetRequiredService<TMain>(), chosen));

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
    /// Adds entries to the menu of the main view model, from a module: a class library that registers its own routes and shows its
    /// own pages in the menu of the application that uses it.
    /// <code>
    /// public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    ///     => services.AddGeneratedRoutes()
    ///                .AddMenuEntries(() => [new RouteableEntry&lt;OrdersViewModel&gt;("Orders", route: "/orders", template: "/orders")]);
    /// </code>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="entries">
    /// Builds the entries. It runs once for each main view model, so each one has entries of its own (a lock or a selection is not
    /// shared between the circuits of a Blazor Server application): return new entries, not ones kept in a field.
    /// </param>
    /// <param name="index">
    /// Where the entries go in the menu: <c>0</c> puts them first, <c>1</c> after the first entry, and so on. It counts the entries the menu has
    /// when the module is added to it (the ones of the main view model and of the modules added before), and a value past the end, like the
    /// default <see langword="null"/>, puts them last. The entries of one module stay together, in the order they were built.
    /// </param>
    /// <remarks>
    /// By default the entries come after the ones of the main view model, in the order the modules were added. This works with
    /// <see cref="RegisterMainViewModel{TMain}"/>, whether a consumer asks for the type given to <c>RegisterMainViewModel</c> or for
    /// <see cref="IMainViewModel"/>. Adding the same module twice adds its entries once (the same <paramref name="entries"/> function);
    /// a function that captures something different on each call counts as another module.
    /// </remarks>
    public static IServiceCollection AddMenuEntries(this IServiceCollection services, Func<IEnumerable<IRouteableEntry>> entries, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(entries);

        // A module added twice (a host that calls it and a helper that calls it too) adds its entries once.
        if (services.Any(d => d.ServiceType == typeof(MenuEntries) && d.ImplementationInstance is MenuEntries known && known.Build.Equals(entries)))
            return services;

        services.AddSingleton(new MenuEntries(entries, index));
        return services;
    }

    // The key under which the container builds the main view model; see RegisterMainViewModel.
    private const string MainKey = "Routeables.MainViewModel";

    private static IMainViewModel WithModuleEntries(IMainViewModel mainViewModel, IServiceProvider provider)
    {
        foreach (var module in provider.GetServices<MenuEntries>())
            module.AddTo(mainViewModel);

        // The modules loaded while running: the ones already loaded add their entries now, the next ones when they are loaded.
        provider.GetService<RouteableModules>()?.Attach(mainViewModel);

        return mainViewModel;
    }

    /// <summary>
    /// Registers the factory that builds the <see cref="ILockCoordinator"/> of a main view model (see
    /// <see cref="CreateLockCoordinator"/>). <c>AddLocking()</c> of <c>Routeables.Locking</c> calls it with its own
    /// coordinator and adds the broker it listens to; call it yourself only to supply another coordinator.
    /// </summary>
    /// <typeparam name="TLockCoordinator">The coordinator; it is built with the provider and the main view model.</typeparam>
    public static void RegisterLockCoordinator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TLockCoordinator>(this IServiceCollection services)
        where TLockCoordinator : ILockCoordinator
    {
        // The factory captures the provider it is resolved from. Scoped, so that provider is the router's own scope.
        services.TryAdd(new ServiceDescriptor(
            typeof(Func<IMainViewModel, ILockCoordinator>),
            sp => (Func<IMainViewModel, ILockCoordinator>)(manager =>
                ActivatorUtilities.CreateInstance<TLockCoordinator>(sp, manager)),
            ServiceLifetime.Scoped));
    }

    /// <summary>
    /// Creates a <see cref="ILockCoordinator"/> bound to <paramref name="mainViewModel"/>
    /// </summary>
    /// <param name="provider">The provider the factory is resolved from.</param>
    /// <param name="mainViewModel">The MVVM manager the coordinator will work with.</param>
    /// <returns>A new <see cref="ILockCoordinator"/> instance (one per call).</returns>
    public static ILockCoordinator GetLockCoordinator(this IServiceProvider provider, IMainViewModel mainViewModel)
    {
        var factory = provider.GetRequiredService<Func<IMainViewModel, ILockCoordinator>>();
        return factory.Invoke(mainViewModel);
    }
}
