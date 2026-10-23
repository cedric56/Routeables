using Routeables.Abstraction;
using Xaminals.Data;
using XAminals.Models;

namespace XAnimals.ViewModels;

/// <remarks>The animal comes from the passed values, or from <paramref name="name"/> when the page is opened from its address (<c>/cats/Manx</c>).</remarks>
public class CatDetailViewModel(IRouter router, string? name = null)
    : AnimalDetailViewModel(router, CatData.Cats.FirstOrDefault(c => c.Name == name)), IInitializeAsync
{
    /// <summary>The values the list passes: declared once, so the name and the type are the same on both sides.</summary>
    public static readonly RouteableKey<Animal> AnimalKey = new("animal");
    public static readonly RouteableKey<Action> BackKey = new("back");

    public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        Animal = args.Get(AnimalKey) ?? Animal;
        Back = args.Get(BackKey);
        return Task.CompletedTask;
    }
}
