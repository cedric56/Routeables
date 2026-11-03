using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using XAnimals.Birds;

namespace XAnimals.Wpf.Birds;

/// <summary>
/// The Birds module for a WPF host, as a module loaded while the application runs: <c>modules.Load("Birds", s => s.AddWpfBirdsModule())</c>,
/// or <c>modules.LoadFrom("XAnimals.Wpf.Birds.dll")</c>, which finds this method by its attribute. A head that references this project
/// at start does not need it: its <c>AddGeneratedRoutes()</c> finds the two pages and <c>XAnimals.Birds</c> adds the menu entry.
/// </summary>
public static class WpfBirdsModule
{
    /// <summary>Registers the two pages, then the menu entry of the shared module.</summary>
    [RouteableModule("Birds")]
    public static IServiceCollection AddWpfBirdsModule(this IServiceCollection services)
        => BirdsModule.AddBirdsModule(services.AddGeneratedRoutes());
}
