using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Locking;
using Routeables.Regions;
using Routeables.Segments;
using XAnimals.Birds;

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

    public static void RegisterViewModels(this IServiceCollection services)
    {
        // The main view model is registered by the head, with UseRouteables<MainViewModel>(...).

        // The coordinator that MainViewModel uses for segments; it lives as long as the routers it drives.
        services.AddRouteCoordinator();

        // The Lock page publishes a RouteableLockMessage: with the broker and the coordinator, the menu locks while a page blocks leaving.
        services.AddLocking();

        // The routes are the [Routeable] attributes of the view models, registered by the head with AddGeneratedRoutes().
        // The details stay Transient (the default): a Scoped or Singleton one would be created once, for the first animal opened.

        services.AddDebugErrorSink();
        services.AddAnimalModules();
        services.AddHistoryRegion();
        services.AddLayoutRegions();

        // Reachable by segments: MainViewModel.GoToAsync("/Bears?SearchText=polar").
        services.RegisterSegment<BearsViewModel>("Bears", "/bears");
    }

    /// <summary>
    /// The modules of the sample: today Birds, whose page is added to the menu after the entries of <see cref="MainViewModel"/>.
    /// The heads do not know them: <see cref="RegisterViewModels"/> calls it, Maui calls it from its own start.
    /// </summary>
    public static IServiceCollection AddAnimalModules(this IServiceCollection services)
        => services.AddBirdsModule();

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
    public static IServiceCollection AddLayoutRegions(this IServiceCollection services)
    {
        services.AddRegionLayout<LayoutController>(LayoutController.Content, LayoutController.Header, LayoutController.Footer, LayoutController.LeftPanel);

        // Reachable by segments, in any region: RouteCoordination.Create().Segment("Bar", region, ("Title", "Cedric")). The credits of
        // the Regions page are one such navigation (see LayoutController.ShowCreditsAsync).
        services.RegisterSegment<LayoutBarViewModel>(LayoutController.BarSegment, "layout-bar");
        services.RegisterSegment<LogoViewModel>(LayoutController.LogoSegment, "shell-logo");

        return services;
    }
}
