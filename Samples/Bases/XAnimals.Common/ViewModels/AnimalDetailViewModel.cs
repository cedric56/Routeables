global using AnimalSelection = (XAminals.Models.Animal Animal, System.Action Back);
using Routeables.Abstraction;
using XAminals.Models;

namespace XAnimals.Common;

/// <summary>
/// What every animal detail page has in common: the animal and the way back to the list.
/// A concrete detail either declares its own <c>[RouteableViewModel]</c> so the navigation is generated per list
/// (animal and back come from the constructor), or implements <see cref="IInitializeAsync{TData}"/> and sets them there.
/// </summary>
public abstract class AnimalDetailViewModel(IRouter router, Animal? animal = null, Action? back = null) : ViewModelBase
{
    private Animal? _animal = animal;
    private Action? _back = back;

    public Animal? Animal
    {
        get => _animal;
        protected set { if (ReferenceEquals(_animal, value)) return; _animal = value; OnPropertyChanged(); }
    }

    public Action? Back
    {
        get => _back;
        protected set { if (ReferenceEquals(_back, value)) return; _back = value; OnPropertyChanged(); }
    }

    public async Task GoBackAsync()
    {
        if (router.CanGoBack)
            // Pop, not Back: the detail leaves the history instead of staying there as a forward entry.
            if ((await router.PopAsync()).Success)
                Back?.Invoke();
    }
}