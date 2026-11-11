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
        // A service of the module that asks for IServiceProvider gets this provider, not the application's: what it resolves later
        // must find the services of the module too.
        if (serviceType == typeof(IServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return new ModuleIsService(module, application.GetService<IServiceProviderIsService>());
        return module.TryGet(serviceType, this, application, out var service) ? service : application.GetService(serviceType);
    }

    // ActivatorUtilities asks which parameters the container can fill: the services of the module count, as well as the application's.
    private sealed class ModuleIsService(RouteableModuleServices module, IServiceProviderIsService? application) : IServiceProviderIsService
    {
        public bool IsService(Type serviceType) => module.Has(serviceType) || application?.IsService(serviceType) == true;
    }

    // The keyed services of a module are not registered (see IRouteableModules); the application's are reachable.
    public object? GetKeyedService(Type serviceType, object? serviceKey)
        => application is IKeyedServiceProvider keyed ? keyed.GetKeyedService(serviceType, serviceKey) : null;

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        => GetKeyedService(serviceType, serviceKey)
           ?? throw new InvalidOperationException($"No service of type '{serviceType}' is registered for the key '{serviceKey}'.");
}

/// <summary>The services a module registered, built when they are asked for (singletons once, the others each time).</summary>
internal sealed class RouteableModuleServices(IEnumerable<ServiceDescriptor> descriptors) : IDisposable, IAsyncDisposable
{
    // Every registration of a type, in the order they were made: the last one answers GetService, all of them answer IEnumerable<T>, as in the container.
    private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptors = descriptors
        .Where(d => !d.IsKeyedService)
        .GroupBy(d => d.ServiceType)
        .ToDictionary(g => g.Key, g => g.ToList());

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
        // Only for a reference type T: the array of T[] of a value type would need code that Native AOT may not have generated.
        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            && serviceType.GetGenericArguments()[0] is { IsValueType: false } element
            && _descriptors.TryGetValue(element, out var all))
        {
            service = BuildArray(element, all, scope, application);
            return true;
        }

        return false;
    }

    // An array of a reference type is the same code for every T (shared generics), so it exists even when Native AOT compiled the application.
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Only called for a reference type: arrays of reference types use the canonical shared code, which AOT always has.")]
    private Array BuildArray(Type element, List<ServiceDescriptor> all, IServiceProvider scope, IServiceProvider application)
    {
        var items = Array.CreateInstance(element, all.Count);
        for (var i = 0; i < all.Count; i++)
            items.SetValue(Resolve(all[i], scope, application), i);
        return items;
    }

    // Transient: built at each request, and owned by whoever asked. Singleton and Scoped: built once. A module loaded while running has no scope
    // of its own (see IRouteableModules), so a scoped service lives as long as the application, which is what the guide says.
    private object Resolve(ServiceDescriptor descriptor, IServiceProvider scope, IServiceProvider application)
    {
        if (descriptor.Lifetime == ServiceLifetime.Transient)
            return Create(descriptor, scope);

        lock (_gate)
        {
            if (!_singletons.TryGetValue(descriptor, out var known))
            {
                // Built against the application's own container, never against one scope of it.
                known = Create(descriptor, new RouteableModuleServiceProvider(application, this));
                _singletons[descriptor] = known;
                if (descriptor.ImplementationInstance is null)          // an instance given by the module is not ours to dispose
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

    // The services the module built once are disposed in the reverse order of their creation, IAsyncDisposable included (a service that
    // has only DisposeAsync is waited for here: prefer DisposeAsync of the module services when you can).
    // Run on the thread pool: a service whose DisposeAsync resumes on the context of the caller (the interface thread) would otherwise wait
    // for the thread that waits for it.
    public void Dispose() => Task.Run(() => DisposeAsync().AsTask()).GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        object[] toDispose;
        lock (_gate)
        {
            toDispose = [.. _created];
            _created.Clear();
            _singletons.Clear();
        }

        // One service that fails to dispose must not keep the others (created before it) alive: every one is tried, the failures are raised together.
        List<Exception>? failures = null;
        for (var i = toDispose.Length - 1; i >= 0; i--)
        {
            try
            {
                switch (toDispose[i])
                {
                    case IAsyncDisposable asyncService: await asyncService.DisposeAsync(); break;
                    case IDisposable service: service.Dispose(); break;
                }
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
    }
}
