using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;

namespace XAnimals.ViewModels;

public class CatsViewModel(IRouter router) : AnimalsViewModel(router, CatData.Cats)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => _router.GoToRouteAsync<CatDetailViewModel>($"/cats/{Uri.EscapeDataString(animal.Name)}",
            RouteableArgs.Create().WithValue(CatDetailViewModel.AnimalKey, animal).WithValue(CatDetailViewModel.BackKey, back));
}
