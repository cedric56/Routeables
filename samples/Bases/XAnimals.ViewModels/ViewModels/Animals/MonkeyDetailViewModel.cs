using Routeables.Abstraction;
using XAnimals.Common;
using XAnimals.Data;

namespace XAnimals.ViewModels;

[RouteableViewModel(typeof(MonkeysViewModel), Route = "/monkeys/{name}")]
[Routeable("/monkeys/{name}")]
public class MonkeyDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, MonkeyData.Monkeys.FirstOrDefault(d => d.Name == name), back);
