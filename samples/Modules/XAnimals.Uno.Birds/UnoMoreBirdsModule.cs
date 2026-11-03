using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Birds;
using XAnimals.Uno.Views;

namespace XAnimals.Uno.Birds;

/// <summary>The More birds module for a Uno host, loaded by the button of the Birds page: <c>viewModel.LoadMoreBirds(s => s.AddUnoMoreBirdsModule())</c>.</summary>
public static class UnoMoreBirdsModule
{
    /// <summary>Registers the page, then the menu entry of the shared module.</summary>
    [RouteableModule(MoreBirdsModule.Name)]
    public static IServiceCollection AddUnoMoreBirdsModule(this IServiceCollection services)
    {
        services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
        return services.AddMoreBirdsModule();
    }
}
