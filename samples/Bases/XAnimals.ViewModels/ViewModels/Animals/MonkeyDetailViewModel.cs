using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using XAnimals.Common;
using XAnimals.Data;

namespace XAnimals.ViewModels;

[RouteableViewModel(typeof(MonkeysViewModel), Route = "/monkeys/{name}")]
[Routeable("/monkeys/{name}")]
[System.ComponentModel.Bindable(true)]
public class MonkeyDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, MonkeyData.Monkeys.FirstOrDefault(d => d.Name == name), back);
