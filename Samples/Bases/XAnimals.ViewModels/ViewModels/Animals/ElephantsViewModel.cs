using Routeables.Abstraction;
using Xaminals.Data;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

public class ElephantsViewModel(IRouter router) : AnimalsViewModel(router, ElephantData.Elephants)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToElephantDetailAsync(_router, animal.Name, back);
}