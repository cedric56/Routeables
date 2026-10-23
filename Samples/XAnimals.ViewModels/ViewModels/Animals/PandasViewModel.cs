using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;

namespace XAnimals.ViewModels;

public class PandasViewModel(IRouter router) : AnimalsViewModel(router, PandaData.Pandas)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
    {
        // {panda::} is an attribute parameter: the name is not given to a constructor, it is set on the property
        // the view declares with its platform's attribute ([Parameter], [QueryProperty], [RouteableParameter]).
        // Route = the registration key and template of the view; Arguments.Route = the address asked for.
        var arguments = RouteableArgs.Create()
            .ForViewModel(back)            
            .WithRoute($"/pandas/{Uri.EscapeDataString(animal.Name)}");

        var routeableView = new RouteableEntry<PandaDetailViewModel>(string.Empty, route: PandaDetailViewModel.Route, template: PandaDetailViewModel.Route)
        {
            Arguments = arguments
        };
        return _router.GoToRouteAsync(routeableView);
    }
}
