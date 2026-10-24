using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>
/// The Birds module. The application loads it with one line, <c>services.AddBirdsModule()</c>, and its page appears in the menu,
/// after the entries of the main view model. Nothing else of the application mentions birds.
/// </summary>
public static class BirdsModule
{
    public const string Route = "/birds";

    public static IServiceCollection AddBirdsModule(this IServiceCollection services)
        => services
            // What the Modules page lists. This is the sample's own record, not something Routeables asks for.
            .AddSingleton(new ModuleInfo("Birds", "Adds the Birds page to the menu.", Route))
            // The entries of the module, built for each main view model. The view of the page is paired with its view model by
            // the [RouteableView] attribute on the view of the head, which AddGeneratedRoutes() of the head registers.
            .AddMenuEntries(() =>
            [
                new RouteableEntry<BirdsViewModel>("Birds", AnimalIcons.Bird, route: Route, template: Route,
                    description: string.Join(Environment.NewLine, "A module", "AddMenuEntries"))
            ]);
}
