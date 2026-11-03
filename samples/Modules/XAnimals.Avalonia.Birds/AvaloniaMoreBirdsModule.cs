using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Birds;

namespace XAnimals.Avalonia.Birds;

/// <summary>The More birds module for an Avalonia host, loaded by the button of the Birds page: <c>ViewModel.LoadMoreBirds(s => s.AddAvaloniaMoreBirdsModule())</c>.</summary>
public static class AvaloniaMoreBirdsModule
{
    /// <summary>Registers the page, then the menu entry of the shared module.</summary>
    [RouteableModule(MoreBirdsModule.Name)]
    public static IServiceCollection AddAvaloniaMoreBirdsModule(this IServiceCollection services)
    {
        services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
        return services.AddMoreBirdsModule();
    }
}
