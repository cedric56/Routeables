using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>
/// The provider a route of a module loaded while running is built with: the services of the module first, then the ones of the
/// application. The container of the application cannot take services after it was built, so the module keeps its own.
/// </summary>
internal sealed class ModuleServiceProvider(IServiceProvider application, ModuleServices module) : IServiceProvider, IKeyedServiceProvider
{
    public object? GetService(Type serviceType)
        => module.TryGet(serviceType, this, application, out var service) ? service : application.GetService(serviceType);

    // The keyed services of a module are not registered (see IRouteableModules); the application's are reachable.
    public object? GetKeyedService(Type serviceType, object? serviceKey)
        => application is IKeyedServiceProvider keyed ? keyed.GetKeyedService(serviceType, serviceKey) : null;

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        => GetKeyedService(serviceType, serviceKey)
           ?? throw new InvalidOperationException($"No service of type '{serviceType}' is registered for the key '{serviceKey}'.");
}

/// <summary>The services a module registered, built when they are asked for (singletons once, the others each time).</summary>
internal sealed class ModuleServices(IEnumerable<ServiceDescriptor> descriptors) : IDisposable
{
    private readonly Dictionary<Type, ServiceDescriptor> _descriptors = descriptors
        .Where(d => !d.IsKeyedService)
        .GroupBy(d => d.ServiceType)
        .ToDictionary(g => g.Key, g => g.Last());          // the last registration wins, as in the container

    private readonly Dictionary<Type, object> _singletons = [];
    private readonly List<object> _created = [];
    private readonly object _gate = new();

    public bool TryGet(Type serviceType, IServiceProvider scope, IServiceProvider application, out object? service)
    {
        service = null;
        if (!_descriptors.TryGetValue(serviceType, out var descriptor))
            return false;

        if (descriptor.Lifetime != ServiceLifetime.Singleton)
        {
            service = Create(descriptor, scope);
            return true;
        }

        lock (_gate)
        {
            if (!_singletons.TryGetValue(serviceType, out var known))
            {
                // A singleton is built against the application's own container, never against one scope of it.
                known = Create(descriptor, new ModuleServiceProvider(application, this));
                _singletons[serviceType] = known;
                if (descriptor.ImplementationInstance is null)          // an instance given by the module is not ours to dispose
                    _created.Add(known);
            }
            service = known;
            return true;
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

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var service in _created.OfType<IDisposable>())
                service.Dispose();
            _created.Clear();
            _singletons.Clear();
        }
    }
}
