using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Locking;
using Routeables.Regions;
using Routeables.Segments;

namespace XAnimals.ViewModels;

public static class Extensions
{
    /// <summary>
    /// In a Debug build, errors that Routeables swallows reach <see cref="DebugErrorSink"/>: the debug output, and a break when a
    /// debugger is attached. A Release build is left alone; an application that registered its own sink keeps it.
    /// Each host calls it (<see cref="RegisterViewModels"/> does, Maui calls it from its own start).
    /// </summary>
    public static IServiceCollection AddDebugErrorSink(this IServiceCollection services)
    {
#if DEBUG
        services.TryAddSingleton<IRouteableErrorSink, DebugErrorSink>();
#endif
        return services;
    }

    public static void RegisterViewModels<
        TCatDetailView,
        TDogDetailView
        >(this IServiceCollection services, ServiceLifetime mainLifetime = ServiceLifetime.Singleton)
        where TCatDetailView : class
        where TDogDetailView : class
    {
        // One main view model per host: Singleton on Avalonia and MAUI, Scoped on Blazor Server.
        services.RegisterMainRouteables<MainViewModel>(mainLifetime);

        // The coordinator that MainViewModel uses for segments; it lives as long as the routers it drives.
        services.AddRouteCoordinator(mainLifetime);

        // The Lock page publishes a RouteableLockMessage: with the broker and the coordinator, the menu locks while a page blocks leaving.
        services.AddLocking();

        // A detail is built for each navigation: a Scoped or Singleton one would be created once, for the first animal
        // opened, and then reused for every other (the instance is cached by key, the later arguments are ignored).
        services.RegisterRoute<TCatDetailView, CatDetailViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/cats/{name}");
        services.RegisterRoute<TDogDetailView, DogDetailViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/dogs/{name}");

        services.AddDebugErrorSink();
        services.AddHistoryRegion();
        services.AddShellRegions();

        // Reachable by segments: MainViewModel.GoToAsync("/Bears?SearchText=polar").
        services.RegisterSegment<BearsViewModel>("Bears", "/bears");
    }

    /// <summary>
    /// The router of the History page: a region, with its own history, that shows real pages of the application in a frame
    /// (a page cannot show the current view of the router that shows it: it would draw itself).
    /// Each host calls it once: <see cref="RegisterViewModels"/> does, Maui calls it from its own start.
    /// </summary>
    public static IServiceCollection AddHistoryRegion(this IServiceCollection services)
        => services.AddRegion(HistoryViewModel.Region);

    /// <summary>
    /// The four regions of the Regions page (Header, Footer, LeftPanel and Content) and the class that drives them.
    /// Each host calls it once: <see cref="RegisterViewModels"/> does, Maui calls it from its own start.
    /// </summary>
    public static IServiceCollection AddShellRegions(this IServiceCollection services)
    {
        services.AddRegionLayout<ShellController>(ShellController.Content, ShellController.Header, ShellController.Footer, ShellController.LeftPanel);

        // Reachable by segments, in any region: RouteCoordination.Create().Segment("Bar", region, ("Title", "Cedric")). The credits of
        // the Regions page are one such navigation (see ShellController.ShowCreditsAsync).
        services.RegisterSegment<ShellBarViewModel>(ShellController.BarSegment, "shell-bar");
        services.RegisterSegment<LogoViewModel>(ShellController.LogoSegment, "shell-logo");

        return services;
    }
}
