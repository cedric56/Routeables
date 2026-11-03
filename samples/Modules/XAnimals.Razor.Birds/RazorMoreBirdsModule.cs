using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Birds;
using XAnimals.Razor.Components.Animals;

namespace XAnimals.Razor.Birds;

/// <summary>The More birds module for a Razor host, loaded by the button of the Birds page: <c>ViewModel.LoadMoreBirds(s => s.AddRazorMoreBirdsModule())</c>.</summary>
public static class RazorMoreBirdsModule
{
    /// <summary>Registers the page, then the menu entry of the shared module.</summary>
    [RouteableModule(MoreBirdsModule.Name)]
    public static IServiceCollection AddRazorMoreBirdsModule(this IServiceCollection services)
    {
        services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
        return services.AddMoreBirdsModule();
    }
}
