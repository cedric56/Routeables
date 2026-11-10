using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using XAnimals.Common;
using XAnimals.Data;
using XAnimals.Models;

namespace XAnimals.ViewModels;

[Routeable("/elephants")]
[System.ComponentModel.Bindable(true)]
public class ElephantsViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, ElephantData.Elephants)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToElephantDetailAsync(_router, animal.Name, back);
}