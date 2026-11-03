using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;

namespace XAnimals.Maui.Birds;

/// <summary>
/// The Birds module for a MAUI host, as a module loaded while the application runs: <c>modules.Load("Birds", s => s.AddMauiBirdsModule())</c>,
/// or <c>modules.LoadFrom("XAnimals.Maui.Birds.dll")</c>, which finds this method by its attribute. A head that references this project
/// at start does not need it: its <c>AddGeneratedRoutes()</c> finds the two pages and <c>XAnimals.Birds</c> adds the menu entry.
/// </summary>
public static class MauiBirdsModule
{
    /// <summary>Registers the two pages, then the menu entry of the shared module.</summary>
    [RouteableModule("Birds")]
    public static IServiceCollection AddMauiBirdsModule(this IServiceCollection services)
        => global::XAnimals.Birds.BirdsModule.AddBirdsModule(services.AddGeneratedRoutes());
}
