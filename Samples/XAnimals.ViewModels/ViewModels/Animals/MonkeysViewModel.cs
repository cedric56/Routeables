using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;

namespace XAnimals.ViewModels;

public class MonkeysViewModel(IRouter router) : AnimalsViewModel(router, MonkeyData.Monkeys)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToMonkeyDetailAsync(_router, animal.Name, back);
}
