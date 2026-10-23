using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Core;

/// <summary>
/// Provides core extension methods for registering runtime environments, synchronizing list collections, 
/// and determining runtime capabilities.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers runtime environment implementations (runtime, desktop info, and distribution details) 
    /// as singletons into the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="runtime">The current runtime environment type.</param>
    public static void RegisterEnvironments(this IServiceCollection services,
        Runtime runtime)
    {
        services.AddSingleton<IRuntime>(new RuntimeImplementation(runtime));
        services.AddSingleton<IDesktop>(new DesktopImplementation());
        services.AddSingleton<IDistribution>(new DistributionImplementation());
    }

    /// <summary>
    /// Determines whether the specified runtime environment represents a Razor-based web or hybrid host 
    /// (such as Blazor Server, Blazor WebAssembly, or MAUI Hybrid).
    /// </summary>
    /// <param name="runtime">The runtime instance to check.</param>
    /// <returns><see langword="true"/> if the runtime is Razor-based; otherwise, <see langword="false"/>.</returns>
    public static bool IsRazorRuntime(this IRuntime runtime)
        => runtime.Current == Runtime.BlazorServer ||
           runtime.Current == Runtime.BlazorWebAssembly ||
           runtime.Current == Runtime.MauiHybrid;
}