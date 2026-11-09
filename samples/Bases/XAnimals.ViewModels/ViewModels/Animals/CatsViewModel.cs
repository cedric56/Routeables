using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Common;
using XAnimals.Data;
using XAnimals.Models;

namespace XAnimals.ViewModels;

[Routeable("/cats")]
[System.ComponentModel.Bindable(true)]
public class CatsViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, CatData.Cats)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => _router.GoToAsync($"/cats/{Uri.EscapeDataString(animal.Name)}",
            RouteableArgs.Create().WithValue(CatDetailViewModel.AnimalKey, animal).WithValue(CatDetailViewModel.BackKey, back));
}
