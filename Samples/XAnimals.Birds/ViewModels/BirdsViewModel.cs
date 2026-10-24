using Routeables.Abstraction;
using Routeables.Routing;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>The list of the Birds module, like the one of the pandas: the tanagers of <see cref="BirdsData"/>.</summary>
public class BirdsViewModel(IRouter router) : AnimalsViewModel(router, BirdsData.Birds)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
    {
        // {bird::} is an attribute parameter: the name is not given to a constructor, it is set on the property
        // the view declares with its platform's attribute ([Parameter], [QueryProperty], [RouteableParameter]).
        var arguments = RouteableArgs.Create()
            .ForViewModel(back)
            .WithRoute($"{BirdsModule.Route}/{Uri.EscapeDataString(animal.Name)}");

        var routeableView = new RouteableEntry<BirdDetailViewModel>(string.Empty, route: BirdDetailViewModel.Route, template: BirdDetailViewModel.Route)
        {
            Arguments = arguments
        };
        return _router.GoToRouteAsync(routeableView);
    }
}
