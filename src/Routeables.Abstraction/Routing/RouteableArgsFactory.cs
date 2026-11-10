namespace Routeables.Abstraction.Routing;

internal class RouteableArgsFactory(Func<IRouteableArgs> factory) : IRouteableArgsFactory
{
    public IRouteableArgs New()
    {
        return factory();
    }
}
