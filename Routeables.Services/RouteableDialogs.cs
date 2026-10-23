using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Routeables.Services;

/// <summary><see cref="IRouteableDialogs"/> over the services of the container; they are resolved when used, so a replacement registered later is the one returned.</summary>
internal sealed class RouteableDialogs(IServiceProvider provider) : IRouteableDialogs
{
    public IRouteableDialogService MessageBox => provider.GetRequiredService<IRouteableDialogService>();

    public IRouteableContentDialogService ContentDialog => provider.GetRequiredService<IRouteableContentDialogService>();

    public IRouteableTaskDialogService TaskDialog => provider.GetRequiredService<IRouteableTaskDialogService>();
}

/// <summary>Registration of <see cref="IRouteableDialogs"/>.</summary>
public static class RouteableDialogsExtensions
{
    /// <summary>
    /// Registers <see cref="IRouteableDialogs"/>. The <c>UseRouteablesServices</c> of each host already calls it; call it yourself
    /// only when you register the three services without it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/>.</returns>
    public static IServiceCollection AddRouteableDialogs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddTransient<IRouteableDialogs, RouteableDialogs>();
        return services;
    }
}
