using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Birds;
using XAnimals.Wpf.Views;

namespace XAnimals.Wpf.Birds;

/// <summary>The More birds module for a WPF host, loaded by the button of the Birds page: <c>viewModel.LoadMoreBirds(s => s.AddWpfMoreBirdsModule())</c>.</summary>
public static class WpfMoreBirdsModule
{
    /// <summary>Registers the page, then the menu entry of the shared module.</summary>
    [RouteableModule(MoreBirdsModule.Name)]
    public static IServiceCollection AddWpfMoreBirdsModule(this IServiceCollection services)
    {
        services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
        return services.AddMoreBirdsModule();
    }
}
