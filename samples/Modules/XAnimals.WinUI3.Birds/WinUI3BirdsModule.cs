using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using XAnimals.Birds;

namespace XAnimals.WinUI3.Birds;

/// <summary>
/// The Birds module for a WinUI 3 host, as a module loaded while the application runs: <c>modules.Load("Birds", s => s.AddWinUI3BirdsModule())</c>,
/// or <c>modules.LoadFrom("XAnimals.WinUI3.Birds.dll")</c>, which finds this method by its attribute. A head that references this project
/// at start does not need it: its <c>AddGeneratedRoutes()</c> finds the two pages and <c>XAnimals.Birds</c> adds the menu entry.
/// </summary>
public static class WinUI3BirdsModule
{
    /// <summary>Registers the two pages, then the menu entry of the shared module.</summary>
    [RouteableModule("Birds")]
    public static IServiceCollection AddWinUI3BirdsModule(this IServiceCollection services)
        => BirdsModule.AddBirdsModule(services.AddGeneratedRoutes());
}
