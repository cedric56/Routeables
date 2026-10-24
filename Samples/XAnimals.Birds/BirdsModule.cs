using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>
/// The Birds module. The application loads it with one line, <c>services.AddBirdsModule()</c>, and its page appears in the menu,
/// after the entries of the main view model. The application code does not mention birds: the module is called from
/// <c>XAnimals.ViewModels</c>, and each head imports the views of <c>Platforms/&lt;head&gt;</c> in its project file.
/// </summary>
public static class BirdsModule
{
    public const string Route = "/birds";

    public static IServiceCollection AddBirdsModule(this IServiceCollection services)
        => services
            // The entries of the module, built for each main view model. The view of the page is paired with its view model by
            // the [RouteableView] attribute on the view of the head, which AddGeneratedRoutes() of the head registers.
            .AddMenuEntries(() =>
            [
                new RouteableEntry<BirdsViewModel>("Birds", AnimalIcons.Bird, route: Route, template: Route,
                    description: string.Join(Environment.NewLine, "A module", "AddMenuEntries"))
            ]);
}
