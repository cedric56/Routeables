using Routeables.Abstraction;
using Xaminals.Data;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>Typed route: the animal arrives through <see cref="IInitializeAsync{TData}"/>, not through the constructor.</summary>
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
