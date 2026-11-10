using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using XAnimals.Common;
using XAnimals.Data;

namespace XAnimals.ViewModels;

/// <summary>Typed route: the animal arrives through <see cref="IInitializeAsync{TData}"/>, not through the constructor.</summary>
[Routeable("/bears/{name}")]
[System.ComponentModel.Bindable(true)]
public class BearDetailViewModel(IRouter router, string? name = null)
    : AnimalDetailViewModel(router, BearData.Bears.FirstOrDefault(b => b.Name == name)), IInitializeAsync<AnimalSelection>
{
    public Task InitializeAsync(AnimalSelection data, CancellationToken ct = default)
    {
        Animal = data.Animal;
        Back = data.Back;
        return Task.CompletedTask;
    }
}
