using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

[Routeable("/cats")]
public class CatsViewModel(IRouter router) : AnimalsViewModel(router, CatData.Cats)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => _router.GoToAsync($"/cats/{Uri.EscapeDataString(animal.Name)}",
            RouteableArgs.Create().WithValue(CatDetailViewModel.AnimalKey, animal).WithValue(CatDetailViewModel.BackKey, back));
}
