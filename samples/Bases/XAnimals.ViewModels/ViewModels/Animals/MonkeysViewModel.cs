using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using XAnimals.Common;
using XAnimals.Data;
using XAnimals.Models;

namespace XAnimals.ViewModels;

[Routeable("/monkeys")]
[System.ComponentModel.Bindable(true)]
public class MonkeysViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, MonkeyData.Monkeys)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToMonkeyDetailAsync(_router, animal.Name, back);
}
