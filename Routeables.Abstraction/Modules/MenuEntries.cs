namespace Routeables.Abstraction;

/// <summary>The entries a module adds to the menu; see <see cref="Extensions.AddMenuEntries"/>.</summary>
internal sealed record MenuEntries(Func<IEnumerable<IRouteableEntry>> Build);
