using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>What a loaded module brought: its name, the assembly it came from (if any) and the routes it registered.</summary>
/// <param name="Name">The name of the module.</param>
/// <param name="Assembly">The simple name of the assembly the module was found in; <c>null</c> for a module given as code.</param>
/// <param name="Routes">The routes the module registered, in registration order.</param>
public sealed record RouteableModuleInfo(string Name, string? Assembly, IReadOnlyList<string> Routes);

/// <summary>
/// Loads modules while the application runs. A module loaded this way adds its routes (they can be reached at once, by the router
/// and from an address), its entries to the menu of every <see cref="IMainViewModel"/> alive, and its own services.
/// <para>
/// A module can also be added at start (<c>services.AddOrdersModule()</c>): then it is part of the container and nothing here is needed.
/// This service is for the module that is not known at start: a feature loaded when the user asks for it, or a DLL found in a folder.
/// </para>
/// <para>
/// Limits of a module loaded while running: it adds routes, menu entries and services (singleton or transient: a scoped service of the
/// module lives as long as the application, and a keyed service or a region of the module is not registered). The container is not rebuilt,
/// so a service of the application that was already built does not see the module. A module is never unloaded. Call <c>Load</c>
/// from the UI thread: the menu is an observable collection.
/// </para>
/// </summary>
public interface IRouteableModules
{
    /// <summary>The modules loaded while running, in the order they were loaded.</summary>
    IReadOnlyList<RouteableModuleInfo> Loaded { get; }

    /// <summary>Raised after a module was loaded and its entries were added to the menus.</summary>
    event EventHandler<RouteableModuleInfo>? ModuleLoaded;

    /// <summary>Whether a module of that name was loaded.</summary>
    bool IsLoaded(string name);

    /// <summary>
    /// Loads a module given as code, with nothing read by reflection: it is safe with trimming and Native AOT. The module is linked in
    /// the application; what is on demand is its activation.
    /// <code>
    /// modules.Load("Orders", services => services.AddOrdersModule());
    /// </code>
    /// </summary>
    /// <param name="name">
    /// The name of the module. Loading a name already loaded does nothing and returns the module that was loaded; if <paramref name="add"/> is
    /// not the method that loaded it, the second one is not run and the mistake is reported to the <see cref="IRouteableErrorSink"/>.
    /// A module that throws, or whose entries cannot be built, is not loaded at all: nothing of it is registered.
    /// </param>
    /// <param name="add">Adds the routes, the menu entries and the services of the module to the collection it is given.</param>
    RouteableModuleInfo Load(string name, Action<IServiceCollection> add);

    /// <summary>Loads the modules of an assembly: each <c>public static</c> method marked <see cref="RouteableModuleAttribute"/>.</summary>
    /// <param name="assembly">The assembly, already loaded.</param>
    [RequiresUnreferencedCode("Finds the methods of the module by reflection: the trimmer cannot see them.")]
    IReadOnlyList<RouteableModuleInfo> Load(Assembly assembly);

    /// <summary>
    /// Loads an assembly from a file and its modules (<see cref="Load(Assembly)"/>). The assemblies the application already has (Routeables
    /// itself, the UI framework) are shared with the module; the others are searched next to the file.
    /// </summary>
    /// <param name="path">The path of the assembly of the module.</param>
    [RequiresUnreferencedCode("Loads an assembly and finds the methods of the module by reflection: the trimmer cannot see them.")]
    IReadOnlyList<RouteableModuleInfo> LoadFrom(string path);
}
