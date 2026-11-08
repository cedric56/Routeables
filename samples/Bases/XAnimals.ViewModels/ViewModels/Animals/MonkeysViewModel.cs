using Routeables.Abstraction;
using XAnimals.Data;
using XAnimals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

[Routeable("/monkeys")]
[System.ComponentModel.Bindable(true)]
public class MonkeysViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, MonkeyData.Monkeys)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToMonkeyDetailAsync(_router, animal.Name, back);
}
