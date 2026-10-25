using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

[Routeable("/bears")]
public class BearsViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, BearData.Bears)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => _router.GoToRouteAsync<BearDetailViewModel, AnimalSelection>(
            $"/bears/{Uri.EscapeDataString(animal.Name)}", (animal, back));
}
