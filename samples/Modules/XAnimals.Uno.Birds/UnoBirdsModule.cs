using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using XAnimals.Birds;

namespace XAnimals.Uno.Birds;

/// <summary>
/// The Birds module for an Uno host, as a module loaded while the application runs: <c>modules.Load("Birds", s => s.AddUnoBirdsModule())</c>,
/// or <c>modules.LoadFrom("XAnimals.Uno.Birds.dll")</c>, which finds this method by its attribute. A head that references this project
/// at start does not need it: its <c>AddGeneratedRoutes()</c> finds the two pages and <c>XAnimals.Birds</c> adds the menu entry.
/// </summary>
public static class UnoBirdsModule
{
    /// <summary>Registers the two pages, then the menu entry of the shared module.</summary>
    [RouteableModule("Birds")]
    public static IServiceCollection AddUnoBirdsModule(this IServiceCollection services)
        => BirdsModule.AddBirdsModule(services.AddGeneratedRoutes());
}
