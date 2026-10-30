using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction;

/// <summary>Registration of routes, views and view models on <see cref="IServiceCollection"/>.</summary>
public static partial class Extensions
{
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
}
