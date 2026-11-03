using Routeables.Abstraction;
using Routeables.Routing;

namespace XAnimals.Birds;

/// <summary>The list of the More birds module: three more tanagers, shown by the detail of the Birds module.</summary>
public class MoreBirdsViewModel(IRouter router, IRouteableModules modules, IServiceProvider provider) : BirdsViewModel(router, modules, provider, MoreBirdsData.Birds)
{
}
