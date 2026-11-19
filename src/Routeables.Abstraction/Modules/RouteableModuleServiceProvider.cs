using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>
/// The provider a route of a module loaded while running is built with: the services of the module first, then the ones of the
/// application. The container of the application cannot take services after it was built, so the module keeps its own.
/// </summary>
internal sealed class RouteableModuleServiceProvider(IServiceProvider application, RouteableModuleServices module) : IServiceProvider, IKeyedServiceProvider
{
    public object? GetService(Type serviceType)
    {
        // A module service that asks for IServiceProvider gets this provider, not the application's: what it resolves later must find the
        // module's services too.
        if (serviceType == typeof(IServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return new ModuleIsService(module, application.GetService<IServiceProviderIsService>());
        return module.TryGet(serviceType, this, application, out var service) ? service : application.GetService(serviceType);
    }

    // ActivatorUtilities asks which parameters the container can fill: the module's services count, as well as the application's.
    private sealed class ModuleIsService(RouteableModuleServices module, IServiceProviderIsService? application) : IServiceProviderIsService
    {
        public bool IsService(Type serviceType) => module.Has(serviceType) || application?.IsService(serviceType) == true;
    }

    // A module's keyed services are not registered (see IRouteableModules); the application's are reachable.
    public object? GetKeyedService(Type serviceType, object? serviceKey)
        => application is IKeyedServiceProvider keyed ? keyed.GetKeyedService(serviceType, serviceKey) : null;

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        => GetKeyedService(serviceType, serviceKey)
           ?? throw new InvalidOperationException($"No service of type '{serviceType}' is registered for the key '{serviceKey}'.");
}

/// <summary>The services a module registered, built when they are asked for (singletons once, the others each time).</summary>
internal sealed class RouteableModuleServices(IEnumerable<ServiceDescriptor> descriptors) : IDisposable, IAsyncDisposable
{
    // Every registration of a type, in registration order: the last one answers GetService, all of them answer IEnumerable<T>, as in the container.
    private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptors = descriptors
        .Where(d => !d.IsKeyedService)
        .GroupBy(d => d.ServiceType)
        .ToDictionary(g => g.Key, g => g.ToList());

    /// <summary>The container of the application itself: what a singleton of the module is built with (null: the provider that asked).</summary>
    public IServiceProvider? Root { get; init; }

    private readonly Dictionary<ServiceDescriptor, object> _singletons = [];
    private readonly List<object> _created = [];
    private readonly object _gate = new();

    public bool Has(Type serviceType) => _descriptors.ContainsKey(serviceType);

    public bool TryGet(Type serviceType, IServiceProvider scope, IServiceProvider application, out object? service)
    {
        service = null;
        if (_descriptors.TryGetValue(serviceType, out var registered))
        {
            service = Resolve(registered[^1], scope, application);
            return true;
        }

        // IEnumerable<T> of a type the module registers: all its registrations (the application's are not mixed in).
        // Only for a reference type T: the array type for a value type T might not have been generated for Native AOT.
        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            && serviceType.GetGenericArguments()[0] is { IsValueType: false } element
            && _descriptors.TryGetValue(element, out var all))
        {
            service = BuildArray(element, all, scope, application);
            return true;
        }

        return false;
    }

    // An array of a reference type uses the same code for every T (shared generics), so it exists even in an application compiled with Native AOT.
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Only called for a reference type: arrays of reference types use the canonical shared code, which AOT always has.")]
    private Array BuildArray(Type element, List<ServiceDescriptor> all, IServiceProvider scope, IServiceProvider application)
    {
        var items = Array.CreateInstance(element, all.Count);
        for (var i = 0; i < all.Count; i++)
            items.SetValue(Resolve(all[i], scope, application), i);
        return items;
    }

    // Transient: built on each request, and owned by the caller. Singleton and Scoped: built once. A module loaded at run time has no scope
    // of its own (see IRouteableModules), so a scoped service lives as long as the application, as the guide says.
    private object Resolve(ServiceDescriptor descriptor, IServiceProvider scope, IServiceProvider application)
    {
        if (descriptor.Lifetime == ServiceLifetime.Transient)
            return Create(descriptor, scope);

        lock (_gate)
        {
            if (!_singletons.TryGetValue(descriptor, out var known))
            {
                // Build against the application's own container, never against one of its scopes: on a server, the asking provider is one visitor's scope,
                // and whatever a singleton took from it would be served to every other visitor.
                known = Create(descriptor, new RouteableModuleServiceProvider(Root ?? application, this));
                _singletons[descriptor] = known;
                if (descriptor.ImplementationInstance is null) // an instance given by the module is not ours to dispose
                    _created.Add(known);
            }
            return known;
        }
    }

    private static object Create(ServiceDescriptor descriptor, IServiceProvider provider)
    {
        if (descriptor.ImplementationInstance is { } instance)
            return instance;
        if (descriptor.ImplementationFactory is { } factory)
            return factory(provider);
        if (descriptor.ImplementationType is { } type)
            return ActivatorUtilities.CreateInstance(provider, type);

        throw new InvalidOperationException($"The module registered '{descriptor.ServiceType}' without a way to build it.");
    }

    // Services the module built once are disposed in reverse order of creation. In a synchronous Dispose, a service with Dispose is disposed
    // as the application's container does; one that only has DisposeAsync is started and not awaited (see AsyncDisposal: blocking could
    // deadlock a thread with a synchronization context, and is impossible on Blazor WebAssembly). Prefer DisposeAsync when you can.
    public void Dispose()
    {
        var failures = new List<Exception>();
        foreach (var service in TakeCreated())
        {
            try
            {
                switch (service)
                {
                    case IDisposable sync: sync.Dispose(); break;
                    case IAsyncDisposable async: Lifecycle.AsyncDisposal.Start<RouteableModuleServices>(async); break;
                }
            }
            catch (Exception ex) { failures.Add(ex); }
        }

        Raise(failures);
    }

    // The services to dispose, last created first; the module forgets them.
    private object[] TakeCreated()
    {
        lock (_gate)
        {
            var taken = _created.AsEnumerable().Reverse().ToArray();
            _created.Clear();
            _singletons.Clear();
            return taken;
        }
    }

    // One service that fails to dispose must not keep the others (created before it) alive: each is tried, and the failures are raised together.
    private static void Raise(List<Exception> failures)
    {
        if (failures.Count > 0)
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
    }

    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var service in TakeCreated())
        {
            try
            {
                switch (service)
                {
                    case IAsyncDisposable asyncService: await asyncService.DisposeAsync(); break;
                    case IDisposable sync: sync.Dispose(); break;
                }
            }
            catch (Exception ex) { failures.Add(ex); }
        }

        Raise(failures);
    }
}
