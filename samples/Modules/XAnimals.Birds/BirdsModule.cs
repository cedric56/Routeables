using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>
/// The Birds module. The application loads it with one line, <c>services.AddBirdsModule()</c>, and its page appears in the menu,
/// after the entries of the main view model. The application code does not mention birds: the module is called from
/// <c>XAnimals.ViewModels</c>, and the views are in the project of each platform (<c>XAnimals.&lt;Platform&gt;.Birds</c>).
/// </summary>
public static class BirdsModule
{
    public const string Route = "/birds";

    public static IServiceCollection AddBirdsModule(this IServiceCollection services)
        => services
            // The entries of the module, built for each main view model. The view of the page is paired with its view model by
            // the [RouteableView] attribute on the view of the platform project, which AddGeneratedRoutes() registers.
            .AddMenuEntries(() =>
            [
                new RouteableEntry<BirdsViewModel>("Birds", AnimalIcons.Bird, route: Route, template: Route,
                    description: string.Join(Environment.NewLine, "A module", "AddMenuEntries"))

            ], 10);
}
