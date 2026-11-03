using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

[Routeable("/monkeys")]
public class MonkeysViewModel(IRouter router, IServiceProvider provider) : AnimalsViewModel(router, provider, MonkeyData.Monkeys)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToMonkeyDetailAsync(_router, animal.Name, back);
}
