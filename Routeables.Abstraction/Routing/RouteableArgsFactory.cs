namespace Routeables.Abstraction;

internal class RouteableArgsFactory(Func<IRouteableArgs> factory) : IRouteableArgsFactory
{
    public IRouteableArgs New()
    {
        return factory();
    }
}
