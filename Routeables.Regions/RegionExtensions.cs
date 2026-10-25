using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Regions;

/// <summary>
/// Registration of a <see cref="RegionLayout"/>.
/// </summary>
public static class RegionExtensions
{
    /// <summary>
    /// Registers a <see cref="RegionLayout"/>: one region per name (see <c>AddRegion</c>) and the layout, scoped. Give the names
    /// the layout passes to its constructor; a <c>RouteableRegion</c> of each name in the view receives it.
    /// </summary>
    public static IServiceCollection AddRegionLayout<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TLayout>(this IServiceCollection services, params string[] regions)
        where TLayout : RegionLayout
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var region in regions)
            services.AddRegion(region);

        services.AddScoped<TLayout>();
        return services;
    }
}
