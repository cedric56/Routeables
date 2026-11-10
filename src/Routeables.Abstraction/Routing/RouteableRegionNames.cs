namespace Routeables.Abstraction.Routing;

/// <summary>The names of the regions registered with <c>AddRegion</c>, so that something that concerns every router (the application going to sleep) can reach them.</summary>
public sealed class RouteableRegionNames
{
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    /// <summary>The registered region names.</summary>
    public IReadOnlyCollection<string> All { get { lock (_names) return _names.ToArray(); } }

    internal void Add(string name) { lock (_names) _names.Add(name); }

    /// <summary>The names recorded in <paramref name="services"/>, created on first use.</summary>
    public static RouteableRegionNames For(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lock (services)
        {
            if (services.FirstOrDefault(d => d.ServiceType == typeof(RouteableRegionNames))?.ImplementationInstance is RouteableRegionNames found)
                return found;
            var created = new RouteableRegionNames();
            services.Add(new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof(RouteableRegionNames), created));
            return created;
        }
    }

    /// <summary>Records a region name.</summary>
    public static void Record(Microsoft.Extensions.DependencyInjection.IServiceCollection services, string name) => For(services).Add(name);
}
