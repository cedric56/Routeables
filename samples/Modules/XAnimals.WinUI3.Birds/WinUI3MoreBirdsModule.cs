using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Birds;
using XAnimals.WinUI3.Views;

namespace XAnimals.WinUI3.Birds;

/// <summary>The More birds module for a WinUI 3 host, loaded by the button of the Birds page: <c>viewModel.LoadMoreBirds(s => s.AddWinUI3MoreBirdsModule())</c>.</summary>
public static class WinUI3MoreBirdsModule
{
    /// <summary>Registers the page, then the menu entry of the shared module.</summary>
    [RouteableModule(MoreBirdsModule.Name)]
    public static IServiceCollection AddWinUI3MoreBirdsModule(this IServiceCollection services)
    {
        services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
        return services.AddMoreBirdsModule();
    }
}
