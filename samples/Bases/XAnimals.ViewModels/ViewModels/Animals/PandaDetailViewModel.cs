using Routeables.Abstraction;
using XAnimals.Data;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// A detail that does not read its panda from a constructor: the view (or, for ASP.NET Core, <see cref="Panda"/>) receives <c>{panda::}</c>
/// from the address in a property decorated with its platform's attribute and gives it to <see cref="Name"/>.
/// </summary>
[Routeable(PandaDetailViewModel.Route)]
public class PandaDetailViewModel(IRouter router, Action? back = null) : AnimalDetailViewModel(router, null, back)
{
    /// <summary>The route and template of the detail; <c>::</c> marks <c>panda</c> as an attribute parameter.</summary>
    public const string Route = "/pandas/{panda::}";

    private string? _name;

    /// <summary>
    /// <c>{panda::}</c> of the address. A host whose view has a property of that name (Blazor, MAUI, Avalonia...) gives it to the view,
    /// which hands it to <see cref="Name"/>; a host whose view cannot have one (ASP.NET Core Razor views) gives it to this property.
    /// </summary>
    [RouteableParameter]
    public string? Panda
    {
        get => Name;
        set => Name = value;
    }

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
