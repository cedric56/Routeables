using Routeables.Abstraction;
using Routeables.Routing;
using XAnimals.Data;
using XAnimals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

[Routeable("/bears")]
[System.ComponentModel.Bindable(true)]
public class BearsViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, BearData.Bears)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => _router.GoToRouteAsync<BearDetailViewModel, AnimalSelection>(
            $"/bears/{Uri.EscapeDataString(animal.Name)}", (animal, back));
}
