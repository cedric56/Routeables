using Routeables.Abstraction;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>
/// The detail of a tanager. Like the one of the pandas, it does not read its bird from a constructor: the view receives
/// <c>{bird::}</c> from the address in a property decorated with its platform's attribute and gives it to <see cref="Name"/>.
/// </summary>
public class BirdDetailViewModel(IRouter router, Action? back = null) : AnimalDetailViewModel(router, null, back)
{
    /// <summary>The route and template of the detail; <c>::</c> marks <c>bird</c> as an attribute parameter.</summary>
    public const string Route = "/birds/{bird::}";

    private string? _name;

    /// <summary>The name of the bird to show; setting it finds the animal.</summary>
    public string? Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            Animal = BirdsData.Birds.FirstOrDefault(bird => bird.Name == value);
            OnPropertyChanged();
        }
    }
}
