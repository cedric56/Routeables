using Routeables.Abstraction;

namespace Routeables.Regions;

/// <summary>
/// What the hosts do when a control of navigation that the application declared (<c>TabControl</c>, <c>NavigationView</c>...) names routes:
/// compares the route of an entry with the one the router shows, and routes without routing twice.
/// </summary>
internal static class RouteTarget
{
    /// <summary>Whether the router shows <paramref name="route"/> (the leading and trailing slashes and the case do not count).</summary>
    internal static bool IsCurrent(IRouter router, string? route)
    {
        ArgumentNullException.ThrowIfNull(router);
        return string.Equals(Trim(router.History.Current?.Args.Route), Trim(route), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Routes to <paramref name="route"/> unless the router shows it already. An error is reported to the sink of the container, not thrown:
    /// this runs from the event of a control.
    /// </summary>
    /// <param name="provider">The container, where the error sink is found.</param>
    /// <param name="router">The router to route.</param>
    /// <param name="route">The route of the entry the user chose.</param>
    /// <param name="owner">The type that asks, named in the report.</param>
    internal static async Task GoAsync(IServiceProvider provider, IRouter router, string route, Type owner)
    {
        try
        {
            if (!IsCurrent(router, route))
                await router.GoToAsync("/" + Trim(route));
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, ex, owner, route);
        }
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim('/');
}
