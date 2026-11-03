using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Birds;
using XAnimals.Maui.Views;

namespace XAnimals.Maui.Birds;

/// <summary>The More birds module for a MAUI host, loaded by the button of the Birds page: <c>viewModel.LoadMoreBirds(s => s.AddMauiMoreBirdsModule())</c>.</summary>
public static class MauiMoreBirdsModule
{
    /// <summary>Registers the page, then the menu entry of the shared module.</summary>
    [RouteableModule(MoreBirdsModule.Name)]
    public static IServiceCollection AddMauiMoreBirdsModule(this IServiceCollection services)
    {
        services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
        return services.AddMoreBirdsModule();
    }
}
