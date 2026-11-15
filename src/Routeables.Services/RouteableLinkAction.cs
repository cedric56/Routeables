using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>
/// What a <c>RouteableLink</c> does when its button is clicked, whatever the host draws the button with: go to the route, and report
/// a failure with <see cref="RouteableErrors"/> instead of letting it end the application (the click handlers are <see langword="async void"/>).
/// Every host calls this one method from its click handler; a host of your own calls it the same way.
/// </summary>
public static class RouteableLinkAction
{
    /// <summary>Goes to <paramref name="route"/> and reports a failure; never throws.</summary>
    /// <param name="provider">The services of the application; <see langword="null"/> when Routeables is not started, which is reported.</param>
    /// <param name="route">The address to go to, such as <c>/hello/World</c>.</param>
    /// <param name="region">The region whose router is used; the default router when <see langword="null"/>.</param>
    public static async Task GoAsync(IServiceProvider? provider, string route, string? region)
    {
        try
        {
            if (provider is null) throw new InvalidOperationException("Routeables is not started: call UseRouteables first.");
            var result = await provider.GoToAsync(route, region);
            if (!result.Success && result.Exception is { } failure)
                RouteableErrors.Report(provider, "RouteableLink", failure);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, "RouteableLink", ex);
        }
    }
}
