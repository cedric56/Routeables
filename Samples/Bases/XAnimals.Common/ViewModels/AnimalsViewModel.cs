using Routeables.Abstraction;
using XAminals.Models;

namespace XAnimals.Common;

/// <summary>
/// What every animal list page has in common: the search, the selection and the hosting of the detail view.
/// A concrete list only gives its animals and the navigation to its own detail view model.
/// </summary>
public abstract class AnimalsViewModel(IRouter router, IServiceProvider provider, IEnumerable<Animal> animals) : ViewModelBase, IRouteable
{
    private string _searchText = string.Empty;
    private object? currentView;
    private bool _blockLeaving;

    protected IRouter _router = router;

    /// <summary>
    /// Whether the list shows its detail inside its own view (<see cref="CurrentView"/>). Not when a region shows this page: the
    /// region displays the current view of its router, which is the detail once the list opened it, and a view has one parent.
    /// </summary>
    public bool HostsDetail { get; private set; } = true;

    /// <summary>
    /// A page sent to a region by its entry (see <c>RegionEntries</c>) is told so by the entry, and a page that a control routes in a
    /// region of its own (the tabs) is not on the default router: in both cases the region shows the detail.
    /// </summary>
    public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        var inRegion = context.Values.Any(v => v.name == PageArguments.RegionKey) || !ReferenceEquals(_router, provider.GetRouter(null));
        HostsDetail = !inRegion;
        return Task.CompletedTask;
    }

    public Animal? SelectedAnimal { get; set; }

    /// <summary>
    /// When set, <see cref="CanLeaveAsync"/> refuses to let the router leave this page (the Navigation page switches it
    /// to show a guard at work).
    /// </summary>
    public bool BlockLeaving
    {
        get => _blockLeaving;
        set
        {
            if (_blockLeaving == value) return;
            _blockLeaving = value;
            OnPropertyChanged(nameof(BlockLeaving));
        }
    }

    /// <summary>The guard: the router asks the page it is leaving, before it changes anything.</summary>
    public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(!BlockLeaving);

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? string.Empty; OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(FilteredAnimals)); }
    }

    public List<Animal> FilteredAnimals =>
        string.IsNullOrWhiteSpace(SearchText)
            ? animals.ToList()
            : animals.Where(a => a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

    public object? CurrentView { get => currentView; set { currentView = value; OnPropertyChanged(nameof(CurrentView)); } }
    public EventHandler<object?>? CurrentViewChanged { get; set; }

    /// <summary>Navigates to the detail view model of this list (the generated <c>GoTo…DetailAsync</c>).</summary>
    protected abstract Task<RouteableResult> GoToDetailAsync(Animal animal, Action back);

    public async Task OnSelectedAnimalAsync(Animal animal)
    {
        SelectedAnimal = animal;

        if ((await GoToDetailAsync(animal, () => RaiseCurrentViewChanged(null))).Success && HostsDetail)
            RaiseCurrentViewChanged(_router.CurrentView);

        void RaiseCurrentViewChanged(object? view)
        {
            CurrentView = view;
            CurrentViewChanged?.Invoke(this, view);
        }
    }
}
