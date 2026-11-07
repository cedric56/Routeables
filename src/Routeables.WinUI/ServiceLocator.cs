#if WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

internal static class ServiceLocator
{
    /// <summary>The provider of the running host: the <c>RouteableRegion</c> of a view finds its router there.</summary>
    public static IServiceProvider? Services { get; set; }
}
