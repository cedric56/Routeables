using Routeables.Abstraction;
using Xaminals.Data;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// A detail that does not read its panda from a constructor: the view receives <c>{panda::}</c> from the address in a property
/// decorated with its platform's attribute and gives it to <see cref="Name"/>.
/// </summary>
[Routeable(PandaDetailViewModel.Route)]
public class PandaDetailViewModel(IRouter router, Action? back = null) : AnimalDetailViewModel(router, null, back)
{
    /// <summary>The route and template of the detail; <c>::</c> marks <c>panda</c> as an attribute parameter.</summary>
    public const string Route = "/pandas/{panda::}";

    private string? _name;

    /// <summary>The name of the panda to show; setting it finds the animal.</summary>
    public string? Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            Animal = PandaData.Pandas.FirstOrDefault(p => p.Name == value);
            OnPropertyChanged();
        }
    }
}
