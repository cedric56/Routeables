namespace Routeables.Abstraction;

/// <summary>
/// The route an application opens when it starts and has no menu to choose it from. Registered by <see cref="Extensions.AddHome"/>.
/// </summary>
/// <param name="Route">The route, for instance <c>"/hello/World"</c>.</param>
public sealed record RouteableHome(string Route);
