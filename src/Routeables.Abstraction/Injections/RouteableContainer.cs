using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction.Injections;

/// <summary>
/// Lets an application build the container of the Routeables hosts that build it themselves (WPF, Avalonia, Uno and WinUI 3) with its own
/// container library (DryIoc, Autofac, Lamar, Grace...) instead of the default one of Microsoft.Extensions.DependencyInjection.
/// </summary>
/// <remarks>
/// <para>
/// Every registration of Routeables is made on the <see cref="IServiceCollection"/>, before the container exists, so any container that reads that collection
/// (the adapter packages of those libraries do) works. MAUI, Blazor and ASP.NET Core build their container in the framework and have their own hook for a
/// container library: <c>MauiAppBuilder.ConfigureContainer</c> and <c>IHostBuilder.UseServiceProviderFactory</c>; this class is not used there.
/// </para>
/// <para>
/// The container must support <b>keyed services</b> (<c>IKeyedServiceProvider</c>: the routes and the regions are registered with a key), scopes
/// (<see cref="IServiceScopeFactory"/>) and the lifetimes of Microsoft.Extensions.DependencyInjection.
/// </para>
/// </remarks>
public static class RouteableContainer
{
    /// <summary>
    /// Builds the container of the host with <paramref name="factory"/>, the <see cref="IServiceProviderFactory{TContainerBuilder}"/> that the container
    /// library gives (<c>new DryIocServiceProviderFactory()</c>). Call it in the block of <c>UseRouteables</c>.
    /// <code>
    /// .UseRouteables(services => services.UseRouteablesContainer(new DryIocServiceProviderFactory()))
    /// </code>
    /// </summary>
    /// <typeparam name="TContainerBuilder">The builder of the container library.</typeparam>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="factory">The factory of the container.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection UseRouteablesContainer<TContainerBuilder>(this IServiceCollection services, IServiceProviderFactory<TContainerBuilder> factory)
        where TContainerBuilder : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        return services.UseRouteablesContainer(collection => factory.CreateServiceProvider(factory.CreateBuilder(collection)));
    }

    /// <summary>
    /// Builds the container of the host with <paramref name="build"/>: for a container that needs registrations of its own, made on the container
    /// after the ones of the collection.
    /// <code>
    /// services.UseRouteablesContainer(collection =>
    /// {
    ///     var container = new Container().WithDependencyInjectionAdapter(collection);
    ///     container.Register&lt;IClock, Clock&gt;(Reuse.Singleton);
    ///     return container.BuildServiceProvider();
    /// });
    /// </code>
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="build">Builds the provider from the collection (what Routeables and the application registered).</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection UseRouteablesContainer(this IServiceCollection services, Func<IServiceCollection, IServiceProvider> build)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(build);
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(ContainerBuilder))
                services.RemoveAt(i);
        services.Add(ServiceDescriptor.Singleton(new ContainerBuilder(build)));
        return services;
    }

    /// <summary>The builder the application chose, or <see langword="null"/> for the default container.</summary>
    internal static Func<IServiceCollection, IServiceProvider>? Find(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(ContainerBuilder) && services[i].ImplementationInstance is ContainerBuilder chosen)
                return chosen.Build;
        return null;
    }

    private sealed record ContainerBuilder(Func<IServiceCollection, IServiceProvider> Build);
}
