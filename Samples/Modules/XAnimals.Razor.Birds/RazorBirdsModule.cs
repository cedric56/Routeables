using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using XAnimals.Razor.Components.Animals;

namespace XAnimals.Razor.Birds;

/// <summary>
/// The Birds module for a Razor host, as a module loaded while the application runs: <c>modules.Load("Birds", s => s.AddRazorBirdsModule())</c>,
/// or <c>modules.LoadFrom("XAnimals.Razor.Birds.dll")</c>, which finds this method by its attribute. A host that references this project
/// at start does not need it: its <c>AddGeneratedRoutes()</c> finds the two components and <c>XAnimals.Birds</c> adds the menu entry.
/// </summary>
public static class RazorBirdsModule
{
    /// <summary>Registers the two components, then the menu entry of the shared module.</summary>
    [RouteableModule("Birds")]
    public static IServiceCollection AddRazorBirdsModule(this IServiceCollection services)
    {
        // The generator reads C# only, and these views are Razor: the routes are registered here, with the same keys it would use.
        services.RegisterRoute<BirdsView, global::XAnimals.Birds.BirdsViewModel>(
            ServiceLifetime.Transient, ServiceLifetime.Transient, global::XAnimals.Birds.BirdsModule.Route);
        services.RegisterRoute<BirdDetailView, global::XAnimals.Birds.BirdDetailViewModel>(
            ServiceLifetime.Transient, ServiceLifetime.Transient, global::XAnimals.Birds.BirdDetailViewModel.Route);

        return global::XAnimals.Birds.BirdsModule.AddBirdsModule(services);
    }
}
