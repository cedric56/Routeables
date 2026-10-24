using Routeables.Abstraction;
using Xaminals.Data;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace Xaminals.ViewModels;

[RouteableViewModel(typeof(MonkeysViewModel), Route = "/monkeys/{name}")]
public class MonkeyDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, MonkeyData.Monkeys.FirstOrDefault(d => d.Name == name), back);
