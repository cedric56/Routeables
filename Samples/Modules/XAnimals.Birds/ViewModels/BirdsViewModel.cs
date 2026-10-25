using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.Birds;

/// <summary>
/// The list of the Birds module, like the one of the pandas: the tanagers of <see cref="BirdsData"/>. It also offers to load
/// the More birds module while the application runs (<see cref="LoadMoreBirds"/>): its view calls it with the method of its
/// platform that adds the module, since the views of a platform are in the project of this platform.
/// </summary>
[Routeable(BirdsModule.Route)]
public class BirdsViewModel : AnimalsViewModel
{
    private readonly IRouteableModules _modules;

    public BirdsViewModel(IRouter router, IRouteableModules modules) : this(router, modules, BirdsData.Birds)
    {
    }

    /// <summary>For the lists that derive from this one: the animals they show. Not used by the container.</summary>
    protected BirdsViewModel(IRouter router, IRouteableModules modules, IEnumerable<Animal> animals) : base(router, animals)
    {
        _modules = modules;
    }

    /// <summary>Whether the More birds module is loaded: its page is in the menu.</summary>
    public bool MoreBirdsLoaded => _modules.IsLoaded(MoreBirdsModule.Name);

    /// <summary>Whether the button that loads the module can be used.</summary>
    public bool CanLoadMoreBirds => !MoreBirdsLoaded;

    /// <summary>
    /// Loads the More birds module: <paramref name="add"/> is the method of the platform that registers its page. Its entry appears in the menu
    /// without restarting the application. Loading it again does nothing.
    /// </summary>
    public void LoadMoreBirds(Action<IServiceCollection> add)
    {
        _modules.Load(MoreBirdsModule.Name, add);
        OnPropertyChanged(nameof(MoreBirdsLoaded));
        OnPropertyChanged(nameof(CanLoadMoreBirds));
    }

    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
    {
        // {bird::} is an attribute parameter: the name is not given to a constructor, it is set on the property
        // the view declares with its platform's attribute ([Parameter], [QueryProperty], [RouteableParameter]).
        var arguments = RouteableArgs.Create()
            .ForViewModel(back)
            .WithRoute($"{BirdsModule.Route}/{Uri.EscapeDataString(animal.Name)}");

        var routeableView = new RouteableEntry<BirdDetailViewModel>(string.Empty, route: BirdDetailViewModel.Route, template: BirdDetailViewModel.Route)
        {
            Arguments = arguments
        };
        return _router.GoToRouteAsync(routeableView);
    }
}
