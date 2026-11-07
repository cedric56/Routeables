using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Common;
using XAnimals.Data;
using XAnimals.Models;

namespace XAnimals.ViewModels;

[Routeable("/pandas")]
public class PandasViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, PandaData.Pandas)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
    {
        // No route string and no menu entry: the router finds the route registered for PandaDetailViewModel
        // ("/pandas/{panda::}") and fills {panda} from the values. {panda::} is an attribute parameter: the name is not given
        // to a constructor, it is set on the property the view declares with its platform's attribute
        // ([Parameter], [QueryProperty], [RouteableParameter]).
        var values = new Dictionary<string, object?> { ["panda"] = animal.Name };
        return _router.GoToAsync<PandaDetailViewModel>(values, RouteableArgs.Create().ForViewModel(back));
    }
}
