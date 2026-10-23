using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;

namespace XAnimals.ViewModels;

public class BearsViewModel(IRouter router) : AnimalsViewModel(router, BearData.Bears)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => _router.GoToRouteAsync<BearDetailViewModel, AnimalSelection>(
            $"/bears/{Uri.EscapeDataString(animal.Name)}", (animal, back));
}
