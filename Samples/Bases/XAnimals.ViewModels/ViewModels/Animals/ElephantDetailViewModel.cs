using Routeables.Abstraction;
using Xaminals.Data;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The elephant is in the address (<c>/elephants/Mammoth</c>): the parameter <c>name</c> is written into the route by the
/// generated method and read back from it by the router, so the page survives a refresh and can be opened from a link.
/// <c>back</c> is optional because a link has no list to return to.
/// </summary>
[RouteableViewModel(typeof(ElephantsViewModel), Route = "/elephants/{name}")]
[Routeable("/elephants/{name}")]
public class ElephantDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, ElephantData.Elephants.FirstOrDefault(e => e.Name == name), back);
