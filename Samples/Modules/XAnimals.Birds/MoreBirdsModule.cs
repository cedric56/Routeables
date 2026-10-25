using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>
/// A module loaded while the application runs. Nothing registers it at start: the button of the Birds page calls
/// <c>IRouteableModules.Load</c> with the method of its platform (<c>AddAvaloniaMoreBirdsModule()</c>...), which registers the page
/// and calls <see cref="AddMoreBirdsModule"/>; its entry then appears in the menu. Its detail is the one of the Birds module.
/// </summary>
public static class MoreBirdsModule
{
    public const string Name = "MoreBirds";
    public const string Route = "/more-birds";

    /// <summary>The menu entry of the module.</summary>
    public static IServiceCollection AddMoreBirdsModule(this IServiceCollection services)
        => services.AddMenuEntries(() =>
        [
            new RouteableEntry<MoreBirdsViewModel>("More birds", AnimalIcons.Bird, route: Route, template: Route,
                description: "A module loaded while the application runs")
        ]);
}
