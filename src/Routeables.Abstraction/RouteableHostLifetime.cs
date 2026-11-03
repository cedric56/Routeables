using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>
/// What a host says about the objects that live as long as its routers (the main view model, the route coordinator):
/// <see cref="ServiceLifetime.Scoped"/> for Blazor, where each circuit has its own routers, and <see cref="ServiceLifetime.Singleton"/>
/// everywhere else. <c>UseRouteables</c> of the host registers it, and <c>RegisterMainViewModel</c> and <c>AddRouteCoordinator</c>
/// read it, so the application does not have to choose.
/// </summary>
/// <param name="Lifetime">Scoped or Singleton.</param>
public sealed record RouteableHostLifetime(ServiceLifetime Lifetime);
