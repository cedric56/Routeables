// One file for every host, which link it (and define WPF or AVALONIA); Routeables.Uno and Routeables.WinUI3 link it too.
#if WPF
namespace Routeables.Wpf;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

internal static class ServiceLocator
{
    /// <summary>The provider of the running host: the <c>RouteableRegion</c> of a view finds its router there.</summary>
    public static IServiceProvider? Services { get; set; }

    /// <summary>Forgets <paramref name="provider"/> when it is the one of the running host: the locator must not keep serving the service of a host that is gone (a second host in the same process, or the next test).</summary>
    public static void Release(IServiceProvider provider)
    {
        if (ReferenceEquals(Services, provider))
            Services = null;
    }
}
