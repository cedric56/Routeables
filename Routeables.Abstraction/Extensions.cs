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
public static class Extensions
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

    private static IMainViewModel WithModuleEntries(IMainViewModel main, IServiceProvider provider)
    {
        foreach (var module in provider.GetServices<MenuEntries>())
            module.AddTo(main);

        // The modules loaded while running: the ones already loaded add their entries now, the next ones when they are loaded.
        provider.GetService<RouteableModules>()?.Attach(main);

        return main;
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
    /// Creates a <see cref="ILockCoordinator"/> bound to <paramref name="routeables"/>
    /// </summary>
    /// <param name="provider">The provider the factory is resolved from.</param>
    /// <param name="routeables">The MVVM manager the coordinator will work with.</param>
    /// <returns>A new <see cref="ILockCoordinator"/> instance (one per call).</returns>
    public static ILockCoordinator GetLockCoordinator(this IServiceProvider provider, IMainViewModel routeables)
    {
        var factory = provider.GetRequiredService<Func<IMainViewModel, ILockCoordinator>>();
        return factory.Invoke(routeables);
    }

    /// <summary>
    /// Populates the injectable properties of <paramref name="view"/> (view or view model):
    /// <list type="number">
    /// <item>A property of type <see cref="RouteablesParameters"/> receives the whole parameter bag.</item>
    /// <item>A property whose name matches an entry in <paramref name="parameters"/> (case-insensitive)
    /// receives that value. Explicit values always win, and <c>null</c> is a valid value.</item>
    /// <item>Any other eligible property that is still <c>null</c> is resolved from DI by its type, when one of its
    /// attributes is registered with <see cref="RouteablePropertyEntry.FromContainer"/> (<c>[Inject]</c>); a value
    /// attribute (<c>[Parameter]</c>, <c>[QueryProperty]</c>) is left unset.</item>
    /// </list>
    /// A property is eligible if it is decorated with one of the attributes registered in
    /// <see cref="IInjection.AttributeTypes"/> (e.g. [Parameter], [Inject]), or if its name is
    /// produced by a registered attribute's <c>getName</c> selector on the view's type (query properties).
    /// Failures on individual properties are logged to the debug output and do not abort injection.
    /// </summary>
    /// <param name="view">The instance to populate.</param>
    /// <param name="sp">The provider used for DI resolution and for the <see cref="IInjection"/> configuration.</param>
    /// <param name="parameters">Optional explicit values, keyed by property name (case-insensitive).</param>
    /// <returns>The same <paramref name="view"/> instance, for chaining.</returns>
    internal static TView Inject<[DynamicallyAccessedMembers(InjectableMembers.Properties)] TView>(this TView view, IServiceProvider sp, IDictionary<string, object?>? parameters = null)
    where TView : notnull
    {
        // Normalise keys so lookups are case-insensitive.
        parameters = parameters is null ? null : ToLastWins(parameters.Select(p => (p.Key.ToLowerInvariant(), p.Value)));

        // No injection configuration registered (or no attributes configured): nothing to do.
        var injection = sp.GetService<IInjection>();
        if (injection is null || injection.AttributeTypes.Count == 0)
            return view;

        // The type as the generator described it, else as reflection reads it (its static type is annotated to keep what that needs). Its type-level attributes (query attributes)
        // make properties injectable even without a property-level attribute.
        var injectable = InjectionTable.Resolve(sp, view);
        var queryPropertyNames = injectable.NamesGivenBy(injection);

        foreach (var property in injectable.Properties)
        {
            // "Context" is reserved (it holds the view's data context); a write-only property cannot be checked or read.
            if (property.Name == "Context" || property.Get is null)
                continue;

            // A RouteablesParameters property receives the entire parameter bag.
            if (parameters != null && property.PropertyType == typeof(RouteablesParameters))
            {
                var bag = new RouteablesParameters();
                foreach (var entry in parameters)
                    bag.Add(entry.Key, entry.Value);
                property.Set(view, bag);
                continue;
            }

            // Only a property decorated with a registered attribute ([Parameter], [Inject]) or named by a type-level one is touched.
            if (!injection.AttributeTypes.Any(entry => property.Has(entry.Type)) && !queryPropertyNames.Contains(property.Name))
                continue;

            // 1) An explicit value wins over anything else (Blazor semantics: null is a valid explicit value).
            if (parameters != null && parameters.TryGetValue(property.Name.ToLowerInvariant(), out var value))
            {
                TrySet(sp, property, view, value, "set parameter");
                continue;
            }

            // 2) Otherwise a property that is still null comes from the container, but only for an attribute that asks for it
            //    ([Inject]): a [Parameter] or a query property without a value stays unset.
            if (injection.AttributeTypes.Any(entry => entry.FromContainer && property.Has(entry.Type)) && property.Get(view) is null)
                TrySet(sp, property, view, () => sp.GetRequiredService(property.PropertyType), "inject");
        }

        return view;
    }

    // A property that cannot be set is reported to the debug output and does not stop the others.
    private static void TrySet(IServiceProvider provider, InjectableProperty property, object view, object? value, string action)
        => TrySet(provider, property, view, () => value, action);

    private static void TrySet(IServiceProvider provider, InjectableProperty property, object view, Func<object?> value, string action)
    {
        try
        {
            property.Set(view, value());
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, $"Failed to {action} '{property.Name}'", ex);
        }
    }

    /// <summary>
    /// The router of a region: the one registered with <c>AddRegion</c> under <paramref name="region"/>, or the default
    /// router when <paramref name="region"/> is <see langword="null"/> or blank.
    /// </summary>
    /// <param name="provider">The provider (scope) to resolve from.</param>
    /// <param name="region">The region's identifier; <see langword="null"/> for the default router.</param>
    /// <exception cref="InvalidOperationException">No region of that name is registered (see <c>AddRegion</c>).</exception>
    public static IRouter GetRouter(this IServiceProvider provider, string? region)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(region))
            return provider.GetRequiredService<IRouter>();

        // The container's own message does not say which key is missing: name the region and how to register it.
        return provider.GetKeyedService<IRouter>(region)
            ?? throw new InvalidOperationException(
                $"No region named '{region}' is registered. Register it with services.AddRegion(\"{region}\"), or use a region name that is registered.");
    }

    /// <summary>
    /// Empties the histories of <paramref name="regions"/>: their pages are disposed and each region starts again from nothing.
    /// Use it to leave an application made of several regions, or to start one over.
    /// </summary>
    public static async Task EraseRegionsAsync(
        this IServiceProvider provider, IEnumerable<string> regions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(regions);
        foreach (var region in regions)
            await provider.GetRequiredKeyedService<IRouter>(region).EraseHistoryAsync(cancellationToken);
    }

    /// <summary>
    /// Goes to a registered route by its address (<c>provider.GoToAsync("/orders/42")</c>), in the router of
    /// <paramref name="region"/>, or else the <see cref="IRouteableArgs.Region"/> of <paramref name="args"/> (the default router when
    /// neither is given). See <see cref="IRouter.GoToAsync"/>.
    /// </summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, string url, string? region = null, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default)
        => provider.GetRouter(region ?? args?.Region).GoToAsync(url, args, template, cancellationToken);

    /// <summary>Goes to a registered route by its address, in the region named by <paramref name="args"/> (<c>RouteableArgs.Create().WithRegion("Form")</c>).</summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, string url, IRouteableArgs args, string? template = null, CancellationToken cancellationToken = default)
        => provider.GoToAsync(url, region: null, args, template, cancellationToken);

    /// <summary>The current view model as a <typeparamref name="T"/>, or <see langword="null"/> when it is another type or none.</summary>
    public static T? GetCurrentViewModel<T>(this IRouter router) where T : class
        => router.CurrentViewModel as T;

    /// <summary>The coordinator that locks <paramref name="routeables"/> while a page blocks leaving; <see langword="null"/> when the router offers none.</summary>
    public static ILockCoordinator? CreateLockCoordinator(this IRouter router, IMainViewModel routeables)
        => (router as ILockCoordinatorFactory)?.CreateLockCoordinator(routeables);

    /// <summary>The router that shows <paramref name="view"/>: the one of its <see cref="IRouteableEntry.Region"/>.</summary>
    public static IRouter RouterFor(this IServiceProvider provider, IRouteableEntry view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return provider.GetRouter(view.Region);
    }

    /// <summary>
    /// Shows <paramref name="view"/> in its region (the default router if it names none). A <c>RouteableRegion</c> given the
    /// same name displays it: the call does not need to know where on screen that is.
    /// </summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, IRouteableEntry view, CancellationToken cancellationToken = default)
        => provider.RouterFor(view).GoToRouteAsync(view, cancellationToken);

    /// <summary>
    /// Registers <typeparamref name="T"/> as a DI-scoped service, once, if no registration for it
    /// exists yet. Use this for state that must not leak between circuits/scopes
    /// (e.g. <see cref="ScopedViewCache"/>).
    /// </summary>
    static void EnsureScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services) where T : class
    {
        if (!services.Any(d => d.ServiceType == typeof(T)))
            services.AddScoped<T>();
    }

    /// <summary>
    /// Creates the view and view model registered for <typeparamref name="TViewModel"/>, binds them
    /// together and shows the view modally.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type; its view must implement <see cref="IRouteableModalFor{TViewModel, TResult}"/>.</typeparam>
    /// <typeparam name="TResult">The result type the modal returns; must match the view's declared result type.</typeparam>
    /// <param name="provider">The provider used to resolve the view and view model.</param>
    /// <param name="args">Optional view / view-model parameters.</param>
    /// <param name="route">Optional route override used to compute the registration keys.</param>
    /// <returns>A task completing with the modal's result (or <c>null</c>/default if dismissed).</returns>
    /// <exception cref="InvalidOperationException">No suitable view is registered, or it is not a modal for the requested types.</exception>
    public static Task<TResult?> ShowModalAsync<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel, TResult>(
        this IServiceProvider provider,
        IRouteableArgs? args = null,
        string? route = null) where TViewModel : class
    {
        var keys = RouteableKeys.GetKeys(typeof(TViewModel), route);

        var viewModel = provider.GetViewModel<TViewModel>(keys.viewModelKey, args);
        var view = provider.GetModalFor<TViewModel, TResult>(args, keys.viewKey) ?? throw new InvalidOperationException($"Unable to create view for {typeof(TViewModel).Name}.  Ensure that it is registered in the container.");
        view.Context = viewModel;
        return view.ShowModalAsync();
    }

    /// <summary>
    /// Registers <paramref name="create"/> as a keyed <em>instance</em>: the resulting
    /// <c>Func&lt;IServiceProvider, RouteableArgs, object&gt;</c> IS the keyed service itself (see the
    /// <c>AddKeyedSingleton&lt;TService&gt;(key, TService instance)</c> overload — not the
    /// <c>Func&lt;IServiceProvider, object?, TService&gt;</c> factory overload). This is
    /// deliberate: callers (<see cref="GetViewModel(IServiceProvider, string, IRouteableArgs?)"/>,
    /// <see cref="GetView"/>) invoke the returned delegate themselves, passing whatever
    /// <see cref="IServiceProvider"/> and arguments they have at hand — the <c>sp</c>/<c>args</c>
    /// below are the delegate's own parameters, supplied per call, never captured from a
    /// DI-resolved registration-time provider.
    /// <para>
    /// The <paramref name="lifetime"/> is implemented by the wrapper, not by DI:
    /// Singleton caches the first instance (later <c>args</c> are ignored), Scoped caches per
    /// <see cref="ScopedViewCache"/> (i.e. per DI scope), Transient creates a new instance each call.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="route">The key under which the factory is registered.</param>
    /// <param name="lifetime">How created instances are cached.</param>
    /// <param name="create">Creates the instance from the caller's provider and arguments.</param>
    private static void RegisterFactory<T>(
        this IServiceCollection services,
        string route,
        ServiceLifetime lifetime,
        Func<IServiceProvider, IRouteableArgs, T> create)
        where T : class
    {
        // Support services needed by the Scoped lifetime and by weak disposal tracking.
        services.EnsureScoped<ScopedViewCache>();
        services.EnsureScoped<WeakDisposableTracker>();
        if (!services.Any(d => d.ServiceType == typeof(SingletonViewCache)))
            services.AddSingleton<SingletonViewCache>();

        Func<IServiceProvider, IRouteableArgs, object> factory = lifetime switch
        {
            ServiceLifetime.Singleton => CreateSingletonFactory(route, create),
            ServiceLifetime.Scoped => (sp, args) =>
            {
                try
                {
                    // One instance per scope, cached by route.
                    return sp.GetRequiredService<ScopedViewCache>()
                        .GetOrCreate(route, () => create(sp, args));
                }
                catch (Exception ex)
                {
                    // Surface the failure to the caller via args, then rethrow.
                    args.Exception = ex;
                    RouteableErrors.Report(sp, $"create scoped '{route}'", ex);
                    throw;
                }
            }
            ,
            ServiceLifetime.Transient => (sp, args) =>
            {
                try
                {
                    return create(sp, args);
                }
                catch (Exception ex)
                {
                    args.Exception = ex;
                    RouteableErrors.Report(sp, $"create transient '{route}'", ex);
                    throw;
                }
            }
            ,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null),
        };

        services.AddKeyedSingleton(route, factory);
    }

    /// <summary>
    /// Builds a factory that creates the instance once and returns it on every later call
    /// (thread-safe, double-checked locking). Note that only the first call's provider and
    /// arguments are used; subsequent calls ignore theirs. If creation throws, nothing is
    /// cached and the next call retries.
    /// </summary>
    /// <param name="route">The registration key, used for diagnostics.</param>
    /// <param name="create">Creates the instance on first use.</param>
    private static Func<IServiceProvider, IRouteableArgs, object> CreateSingletonFactory<T>(
        string route,
        Func<IServiceProvider, IRouteableArgs, T> create)
        where T : class
    {
        // The instance is kept by the provider (SingletonViewCache), not by this delegate: a delegate registered in the
        // collection is shared by every provider built from it, and nobody disposes what it holds.
        return (sp, args) =>
        {
            try
            {
                return sp.GetRequiredService<SingletonViewCache>().GetOrCreate(route, () => create(sp, args));
            }
            catch (Exception ex)
            {
                args.Exception = ex;
                // Log the registration key (ViewModel type name, etc.), not only T
                RouteableErrors.Report(sp, $"create singleton '{route}' ({typeof(T).Name})", ex);
                throw;
            }
        };
    }

    /// <summary>
    /// Registers a route backed by a custom factory. Use when the view cannot be built by DI alone.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="route">The route key.</param>
    /// <param name="factory">Creates the view from the provider and routing arguments.</param>
    /// <param name="lifetime">Instance lifetime (default <see cref="ServiceLifetime.Transient"/>).</param>
    public static void RegisterRoute(this IServiceCollection services, string route,
        Func<IServiceProvider, IRouteableArgs, object> factory,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        services.RegisterFactory(route, lifetime, factory);
        services.AddAddressableRoute(route);
    }

    /// <summary>
    /// Registers a route for a view type only (no paired view model). The view is created with
    /// <see cref="ActivatorUtilities"/> and then injected with the attribute parameters.
    /// </summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="route">The route key / template.</param>
    /// <param name="lifetime">Instance lifetime (default <see cref="ServiceLifetime.Transient"/>).</param>
    public static void RegisterRoute<[DynamicallyAccessedMembers(InjectableMembers.Created)] TView>(
        this IServiceCollection services, string route, ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TView : class
    {
        services.RegisterFactory(route, lifetime, (sp, args) => ActivatorUtilities.CreateInstance<TView>(sp).InjectView(sp, args));
        services.AddAddressableRoute(route);
    }

    /// <summary>
    /// Shared implementation of the view / view-model registration overloads: registers the view
    /// model under its key (with <paramref name="viewModelLifetime"/>) and the view under its key
    /// (with <paramref name="viewLifetime"/>). Keys are derived from <typeparamref name="TViewModel"/>
    /// by <see cref="RouteableKeys.GetKeys"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="createView">Creates the view from the provider and arguments.</param>
    /// <param name="viewModelLifetime">Lifetime of the view model.</param>
    /// <param name="viewLifetime">Lifetime of the view.</param>
    /// <param name="route">Optional route override used to compute the keys.</param>
    private static void RegisterPageCore<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel>(
        this IServiceCollection services,
        Func<IServiceProvider, IRouteableArgs, TView> createView,
        ServiceLifetime viewModelLifetime,
        ServiceLifetime viewLifetime,
        string? route = null)
        where TView : class where TViewModel : class
        => services.RegisterPage<TView, TViewModel>(
            createView, key => services.RegisterViewModel<TViewModel>(key, viewModelLifetime), viewLifetime, route);

    /// <summary>
    /// Registers a view / view-model pair whose view and view model are built by the factories the source generator wrote for them:
    /// nothing is read by reflection. It is meant for the generated code.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="viewFactory">Creates the view from the provider and arguments, injected.</param>
    /// <param name="viewModelFactory">Creates the view model from the provider and arguments, injected.</param>
    /// <param name="viewModelLifetime">Lifetime of the view model.</param>
    /// <param name="viewLifetime">Lifetime of the view.</param>
    /// <param name="route">Optional route.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterRoute<TView, TViewModel>(
        this IServiceCollection services,
        Func<IServiceProvider, IRouteableArgs, TView> viewFactory,
        Func<IServiceProvider, IRouteableArgs, TViewModel> viewModelFactory,
        ServiceLifetime viewModelLifetime,
        ServiceLifetime viewLifetime,
        string? route = null)
        where TView : class where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(viewFactory);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        services.RegisterPage<TView, TViewModel>(
            viewFactory, key => services.RegisterFactory(key, viewModelLifetime, viewModelFactory), viewLifetime, route);
    }

    private static void RegisterPage<TView, TViewModel>(
        this IServiceCollection services,
        Func<IServiceProvider, IRouteableArgs, TView> createView,
        Action<string> registerViewModel,
        ServiceLifetime viewLifetime,
        string? route)
        where TView : class where TViewModel : class
    {
        var keys = RouteableKeys.GetKeys(typeof(TViewModel), route);
        registerViewModel(keys.viewModelKey);
        services.RegisterFactory(keys.viewKey, viewLifetime, createView);

        // A route given by the caller ("about", "/customers/{id:int}") can be reached from an address (Blazor URL,
        // deep link): remember how to go to it. The default keys (no route given) are not addresses.
        if (!string.IsNullOrWhiteSpace(route))
            services.GetRouteableTable().Add(route, typeof(TViewModel));
    }

    // A route without a view model (a view only, or a custom factory): the view is found under the route itself.
    private static void AddAddressableRoute(this IServiceCollection services, string route)
    {
        if (!string.IsNullOrWhiteSpace(route))
            services.GetRouteableTable().Add(route, null);
    }

    // One table per collection, found through its instance registration so the order of calls does not matter.
    private static RouteableTable GetRouteableTable(this IServiceCollection services)
    {
        var table = services.Select(d => d.ImplementationInstance).OfType<RouteableTable>().FirstOrDefault();
        if (table is null)
        {
            table = new RouteableTable();
            services.AddSingleton(table);
        }
        return table;
    }

    /// <summary>
    /// Registers a view / view-model pair whose view takes one positional constructor argument.
    /// The argument is read from the first view parameter (index 0); with <paramref name="func"/> the
    /// view is built manually, otherwise by DI with the view parameters as extra constructor arguments.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="viewModelLifetime">Lifetime of the view model.</param>
    /// <param name="viewLifetime">Lifetime of the view.</param>
    /// <param name="route">Optional route override.</param>
    /// <param name="func">Optional custom view factory receiving the provider and the argument.</param>
    /// <remarks>Unlike the other positional overloads, <paramref name="func"/> comes last here.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterRoute<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel,
        TArg1>(
        this IServiceCollection services,
        ServiceLifetime viewModelLifetime = ServiceLifetime.Transient,
        ServiceLifetime viewLifetime = ServiceLifetime.Transient,
        string? route = null,
        Func<IServiceProvider, TArg1, TView>? func = null)
        where TView : class where TViewModel : class
    {
        services.RegisterPageCore<TView, TViewModel>(
            (sp, args) =>
            {
                if (func != null)
                    return func(sp, args.ViewParameters.Resolve<TArg1>(0)).InjectView(sp, args);
                return ResolveView<TView>(sp, args);   // injected by ResolveView

            }, viewModelLifetime, viewLifetime, route);
    }

    /// <summary>
    /// Injects a view: the attribute entries routed to the view, plus the attribute entries routed to the view model
    /// (<c>{name::}</c> in a template, which sends the value to properties rather than to a constructor) whose name is the
    /// name of a property of the view decorated with a registered attribute ([Parameter], [QueryProperty], [RouteableParameter]).
    /// A value without such a property on the view is left to the view model, so a view never receives what it did not ask for.
    /// </summary>
    internal static TView InjectView<[DynamicallyAccessedMembers(InjectableMembers.Properties)] TView>(this TView view, IServiceProvider sp, IRouteableArgs args)
        where TView : notnull
    {
        var parameters = args.ViewParameters.ResolveAttributesParameters();

        var injection = sp.GetService<IInjection>();
        if (injection is not null && injection.AttributeTypes.Count > 0 && args.ViewModelParameters.Any(e => e.attribute && e.name is not null))
        {
            var names = view.InjectablePropertyNames(sp, injection);
            foreach (var entry in args.ViewModelParameters)
                if (entry.attribute && entry.name is not null && names.Contains(entry.name) && !parameters.ContainsKey(entry.name))
                    parameters[entry.name] = entry.value;
        }

        return view.Inject(sp, parameters);
    }

    // The properties a view declares as injectable, ignoring the ones that hold the whole bag or the data context.
    private static HashSet<string> InjectablePropertyNames<[DynamicallyAccessedMembers(InjectableMembers.Properties)] TView>(this TView view, IServiceProvider sp, IInjection injection)
        where TView : notnull
    {
        var injectable = InjectionTable.Resolve(sp, view);
        var names = injectable.NamesGivenBy(injection);

        foreach (var property in injectable.Properties)
        {
            if (property.Name == "Context" || property.PropertyType == typeof(RouteablesParameters))
                continue;
            if (injection.AttributeTypes.Any(a => property.Has(a.Type)))
                names.Add(property.Name);
        }

        return names;
    }

    /// <summary>
    /// Extracts the attribute-bound entries (e.g. query/[Parameter] values) as a name → value map.
    /// </summary>
    internal static Dictionary<string, object?> ResolveAttributesParameters(this IList<RouteableArgument> parameters)
        => ToLastWins(parameters.Where(p => p.attribute == true).Select(p => (p.name!, p.value)));

    // A name given twice (a value passed by hand and the one of the address, "Tab" and "tab") is not an error: the last one is used.
    private static Dictionary<string, object?> ToLastWins(IEnumerable<(string name, object? value)> pairs)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (name, value) in pairs)
            result[name] = value;
        return result;
    }

    /// <summary>
    /// Extracts the positional (non-attribute) values, skipping nulls, to be passed as extra
    /// constructor arguments to <see cref="ActivatorUtilities"/>.
    /// </summary>
    private static object[] ResolveViewParameters(this IList<RouteableArgument> parameters)
        => parameters.Where(p => p.attribute == false && p.value is not null).Select(p => p.value!).ToArray();

    /// <summary>
    /// Gets the entry at <paramref name="index"/> cast to <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="InvalidCastException">The value is null or not a <typeparamref name="T"/>.</exception>
    private static T Resolve<T>(this IList<RouteableArgument> args, int index)
    {
        if (index < 0 || index >= args.Count)
            throw new ArgumentException($"Invalid index : {index} but length is : {args.Count}");
        var value = args[index].value;
        return value is T t ? t :
            throw new InvalidCastException($"Excepted argument is type of : {typeof(T)} but resolved : {value?.GetType()}");
    }

    /// <summary>
    /// Registers a view / view-model pair whose view takes two positional constructor arguments
    /// (view parameters 0 and 1). See the single-argument overload for details.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterRoute<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel,
        TArg1, TArg2>(
        this IServiceCollection services,
        Func<IServiceProvider, TArg1, TArg2, TView>? func = null,
        ServiceLifetime viewModelLifetime = ServiceLifetime.Transient,
        ServiceLifetime viewLifetime = ServiceLifetime.Transient,
        string? route = null)
        where TView : class where TViewModel : class
        => services.RegisterPageCore<TView, TViewModel>(
            (sp, args) =>
            {
                return func is not null ?
                    func(sp, args.ViewParameters.Resolve<TArg1>(0), args.ViewParameters.Resolve<TArg2>(1)).InjectView(sp, args) :
                    ResolveView<TView>(sp, args);   // injected by ResolveView

            }, viewModelLifetime, viewLifetime, route);

    /// <summary>
    /// Registers a view / view-model pair whose view takes three positional constructor arguments
    /// (view parameters 0 to 2). See the single-argument overload for details.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterRoute<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel,
        TArg1, TArg2, TArg3>(
        this IServiceCollection services,
        Func<IServiceProvider, TArg1, TArg2, TArg3, TView>? func = null,
        ServiceLifetime viewModelLifetime = ServiceLifetime.Transient,
        ServiceLifetime viewLifetime = ServiceLifetime.Transient,
        string? route = null)
        where TView : class where TViewModel : class
        => services.RegisterPageCore<TView, TViewModel>(
            (sp, args) =>
            {
                return func is not null ?
                    func(sp, args.ViewParameters.Resolve<TArg1>(0), args.ViewParameters.Resolve<TArg2>(1), args.ViewParameters.Resolve<TArg3>(2)).InjectView(sp, args) :
                    ResolveView<TView>(sp, args);   // injected by ResolveView
            },
            viewModelLifetime, viewLifetime, route);

    /// <summary>
    /// Registers a view / view-model pair whose view takes four positional constructor arguments
    /// (view parameters 0 to 3). See the single-argument overload for details.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterRoute<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel,
        TArg1, TArg2, TArg3, TArg4>(
        this IServiceCollection services,
        Func<IServiceProvider, TArg1, TArg2, TArg3, TArg4, TView>? func = null,
        ServiceLifetime viewModelLifetime = ServiceLifetime.Transient,
        ServiceLifetime viewLifetime = ServiceLifetime.Transient,
        string? route = null)
        where TView : class where TViewModel : class
        => services.RegisterPageCore<TView, TViewModel>(
            (sp, args) =>
            {
                return func is not null ?
                    func(sp, args.ViewParameters.Resolve<TArg1>(0), args.ViewParameters.Resolve<TArg2>(1), args.ViewParameters.Resolve<TArg3>(2), args.ViewParameters.Resolve<TArg4>(3)).InjectView(sp, args) :
                    ResolveView<TView>(sp, args);   // injected by ResolveView
            },
            viewModelLifetime, viewLifetime, route);

    /// <summary>
    /// Registers a view / view-model pair whose view takes five positional constructor arguments
    /// (view parameters 0 to 4). See the single-argument overload for details.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterRoute<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel,
        TArg1, TArg2, TArg3, TArg4, TArg5>(
        this IServiceCollection services,
        Func<IServiceProvider, TArg1, TArg2, TArg3, TArg4, TArg5, TView>? func = null,
        ServiceLifetime viewModelLifetime = ServiceLifetime.Transient,
        ServiceLifetime viewLifetime = ServiceLifetime.Transient,
        string? route = null)
        where TView : class where TViewModel : class
        => services.RegisterPageCore<TView, TViewModel>(
            (sp, args) =>
            {
                return func is not null ?
                    func(sp, args.ViewParameters.Resolve<TArg1>(0), args.ViewParameters.Resolve<TArg2>(1), args.ViewParameters.Resolve<TArg3>(2), args.ViewParameters.Resolve<TArg4>(3), args.ViewParameters.Resolve<TArg5>(4)).InjectView(sp, args) :
                    ResolveView<TView>(sp, args);   // injected by ResolveView
            },
            viewModelLifetime, viewLifetime, route);

    /// <summary>
    /// Registers a view / view-model pair with no positional arguments. The optional
    /// <paramref name="func"/> receives the full <see cref="IRouteableArgs"/> and replaces the
    /// default DI-based view creation.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="viewModelLifetime">Lifetime of the view model.</param>
    /// <param name="viewLifetime">Lifetime of the view.</param>
    /// <param name="route">Optional route override.</param>
    /// <param name="func">Optional custom view factory.</param>
    public static void RegisterRoute<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel>(
        this IServiceCollection services,
        ServiceLifetime viewModelLifetime = ServiceLifetime.Transient,
        ServiceLifetime viewLifetime = ServiceLifetime.Transient,
        string? route = null,
        Func<IServiceProvider, IRouteableArgs, TView>? func = null)
        where TView : class where TViewModel : class
        => services.RegisterPageCore<TView, TViewModel>(
            (sp, args) => func is not null ? func(sp, args).InjectView(sp, args) : ResolveView<TView>(sp, args)
            , viewModelLifetime, viewLifetime, route);

    /// <summary>
    /// Creates a view through DI (positional view parameters become extra constructor arguments),
    /// injects its attribute properties.
    /// </summary>
    private static TView ResolveView<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView>(IServiceProvider sp, IRouteableArgs args)
        where TView : class
    {
        var instance = ActivatorUtilities.CreateInstance<TView>(sp,
            args.ViewParameters.ResolveViewParameters()).
            InjectView(sp, args);
        // Not tracked here: GetView tracks what it gets back, unless a cache owns it.
        return instance;
    }

    /// <summary>
    /// The values for the constructor, in the order that gives each to the parameter of its name: <c>WithViewModel("id", 42)</c>
    /// goes to the constructor parameter <c>id</c>, whatever the order of the calls and the types of the other parameters. Values
    /// without a name, or whose name no constructor parameter has, keep their order after the named ones.
    /// </summary>
    private static List<object> BindByName(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, IEnumerable<RouteableArgument> entries)
        => BindByName(entries, type.GetConstructors()
            .Select(c => c.GetParameters().Select((p, i) => (p.Name, i)).ToDictionary(x => x.Name!, x => x.i, StringComparer.OrdinalIgnoreCase)));

    /// <summary>The same, given the position of each parameter name in each constructor that could be called.</summary>
    internal static List<object> BindByName(IEnumerable<RouteableArgument> entries, IEnumerable<Dictionary<string, int>> constructors)
    {
        var list = entries.ToList();
        var names = list.Where(e => !string.IsNullOrEmpty(e.name)).Select(e => e.name!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
            return [.. list.Select(e => e.value!)];

        // The constructor that has the most of the names; its parameter order is the order of the values.
        var order = constructors
            .OrderByDescending(d => names.Count(d.ContainsKey))
            .FirstOrDefault();

        int Position(RouteableArgument e)
            => order is not null && !string.IsNullOrEmpty(e.name) && order.TryGetValue(e.name, out var i) ? i : int.MaxValue;

        // OrderBy is stable: the values that are not named by a parameter stay in the order they were given.
        return [.. list.OrderBy(Position).Select(e => e.value!)];
    }

    private static bool TakesRouter([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
        => type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IRouter)));

    /// <summary>
    /// Registers a view model as a route-only entry (see <see cref="RegisterFactory"/>). It is created
    /// through DI with the non-attribute view-model parameters as extra constructor arguments, then
    /// injected with the attribute parameters.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="route">The view-model key.</param>
    /// <param name="lifetime">Instance lifetime (default <see cref="ServiceLifetime.Singleton"/>).</param>
    private static void RegisterViewModel<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel>(
        this IServiceCollection services,
        string route,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TViewModel : class
        => services.RegisterFactory(route, lifetime, (sp, args) =>
        {
            var parameters = BindByName(typeof(TViewModel), args.ViewModelParameters
                .Where(p => p.attribute == false && p.value is not null));

            // The router that routes is the one the view model receives, when it takes one and none was given: a page shown
            // in a region routes that region. (Extra arguments that no constructor parameter takes are refused by
            // ActivatorUtilities, hence the check.)
            if (args.Router is { } router && !parameters.Any(p => p is IRouter) && TakesRouter(typeof(TViewModel)))
                parameters.Add(router);

            return ActivatorUtilities.CreateInstance<TViewModel>(sp, [.. parameters])
                .Inject(sp, args.ViewModelParameters.ResolveAttributesParameters());
        });

    /// <summary>
    /// Typed convenience wrapper over <see cref="GetViewModel(IServiceProvider, string, IRouteableArgs?)"/>.
    /// </summary>
    /// <typeparam name="T">The expected view-model type.</typeparam>
    /// <exception cref="InvalidCastException">The resolved instance is not a <typeparamref name="T"/>.</exception>
    public static T? GetViewModel<T>(this IServiceProvider provider, string route, IRouteableArgs? args = null)
        => (T?)GetViewModel(provider, route, args);

    // The factory of a view or view model: the one of the container, or else the one of a module loaded while running, which is built
    // with the services of the module on top of the application's.
    private static Func<IServiceProvider, IRouteableArgs, object>? FindFactory(this IServiceProvider provider, object key, out IServiceProvider factoryProvider)
    {
        var factory = provider.GetKeyedService<Func<IServiceProvider, IRouteableArgs, object>>(key);
        if (factory is not null)
        {
            factoryProvider = provider;
            return factory;
        }

        if (provider.GetService<RouteableModules>() is { } modules && modules.TryGetFactory(key, provider, out var late, out var moduleProvider))
        {
            factoryProvider = moduleProvider;
            return late;
        }

        factoryProvider = provider;
        return null;
    }

    // Weak tracking is for what nobody owns (transient pages). A singleton or scoped page belongs to its cache, which
    // disposes it: tracking it too would dispose it twice.
    private static void TrackUnlessCached(this IServiceProvider provider, object? instance)
    {
        if (instance is null) return;
        if (provider.GetService<ScopedViewCache>()?.Owns(instance) == true) return;
        if (provider.GetService<SingletonViewCache>()?.Owns(instance) == true) return;
        provider.GetService<WeakDisposableTracker>()?.Track(instance);
    }

    /// <summary>
    /// Resolves a view model through the keyed factory registered for <paramref name="route"/>
    /// and tracks it for weak disposal.
    /// </summary>
    /// <param name="provider">The provider passed to the factory (determines scope).</param>
    /// <param name="route">The view-model registration key.</param>
    /// <param name="args">Arguments for the factory; a new empty <see cref="IRouteableArgs"/> when omitted.</param>
    /// <exception cref="InvalidOperationException">Nothing is registered for the key, or the factory returned null.</exception>
    public static object? GetViewModel(this IServiceProvider provider, string route, IRouteableArgs? args = null)
    {
        var factory = provider.FindFactory(route, out var factoryProvider);
        if (factory != null)
        {
            var instance = factory(factoryProvider, args ?? provider.GetRequiredService<IRouteableArgsFactory>().New());
            provider.TrackUnlessCached(instance);

            return instance;
        }
        return null;
    }

    /// <summary>
    /// Resolves a view through the keyed factory registered for <paramref name="key"/>
    /// and tracks it for weak disposal.
    /// </summary>
    /// <param name="provider">The provider passed to the factory (determines scope).</param>
    /// <param name="key">The view registration key.</param>
    /// <param name="args">Arguments for the factory; a new empty <see cref="IRouteableArgs"/> when omitted.</param>
    /// <exception cref="InvalidOperationException">No view is registered for the key, or the factory returned null.</exception>
    internal static object? GetView(this IServiceProvider provider, object key, IRouteableArgs? args = null)
    {
        var factory = provider.FindFactory(key, out var factoryProvider);
        if (factory != null)
        {
            var instance = factory(factoryProvider, args ?? provider.GetRequiredService<IRouteableArgsFactory>().New())
            ?? throw new InvalidOperationException($"Unable to create view for {key}. Ensure that it is registered with the service provider.");

            // Track with weak reference
            provider.TrackUnlessCached(instance);

            return instance;
        }
        return null;
    }

    /// <summary>
    /// Resolves the view for <typeparamref name="TViewModel"/>, keyed by the view model's type name.
    /// </summary>
    public static IRouteableFor? GetContextFor<TViewModel>(this IServiceProvider provider, IRouteableArgs? args = null)
    {
        var keys = RouteableKeys.GetKeys(typeof(TViewModel));
        return provider.GetView(keys.viewKey, args) as IRouteableFor;
    }

    /// <summary>
    /// Creates the content registered under <paramref name="key"/>, passing
    /// <paramref name="context"/> as the single view parameter (under <c>Constants.Key</c>).
    /// Unlike <see cref="GetView"/>, the result is not tracked for disposal.
    /// </summary>
    /// <param name="provider">The provider passed to the factory.</param>
    /// <param name="key">The registration key.</param>
    /// <param name="context">The context object handed to the factory.</param>
    /// <exception cref="InvalidOperationException">No view is registered for the key, or the factory returned null.</exception>
    public static object GetContent(this IServiceProvider provider, object key, object context)
    {
        var factory = provider.FindFactory(key, out var factoryProvider)
            ?? throw new InvalidOperationException($"No such view registered: {key}");

        return factory(factoryProvider, provider.GetRequiredService<IRouteableArgsFactory>().New().ForView(
            new RouteableArgument(context, Constants.Key)))
            ?? throw new InvalidOperationException($"Unable to create view for {key}. Ensure that it is registered with the service provider.");
    }

    /// <summary>
    /// Resolves the view for <typeparamref name="TViewModel"/> as a modal returning
    /// <typeparamref name="TResult"/>. A view that is not that modal (its result type is another one, say) throws an exception
    /// that names the types.
    /// </summary>
    /// <param name="provider">The provider used to resolve the view.</param>
    /// <param name="args">Optional routing arguments.</param>
    /// <param name="route">Optional view key; defaults to the view model's type name.</param>
    /// <exception cref="InvalidOperationException">The view's result type mismatches, or it is not a modal for these types.</exception>
    public static IRouteableModalFor<TViewModel, TResult>? GetModalFor<TViewModel, TResult>(
        this IServiceProvider provider, IRouteableArgs? args = null, string? route = null)
        where TViewModel : class
    {
        var view = provider.GetView(route ?? typeof(TViewModel).Name, args);

        // The closed interface is the whole check: a view whose result type is another one is not this modal.
        return view as IRouteableModalFor<TViewModel, TResult>
            ?? throw new InvalidOperationException(
                $"The view associated with the view model {typeof(TViewModel).Name} ({view?.GetType().Name ?? "none"}) is not a " +
                $"{nameof(IRouteableModalFor<TViewModel, TResult>)}<{typeof(TViewModel).Name}, {typeof(TResult).Name}>: check its result type.");
    }
}