using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>
/// <see cref="IRouteableModules"/>: runs the method of a module on a collection of its own, then takes from it what a built container
/// cannot take any more: the route factories (found by <see cref="Extensions"/> when the application's container has none for a key),
/// the addressable routes (merged in the table of the application), the menu entries (added to every main alive) and the services.
/// </summary>
internal sealed class RouteableModules(IServiceProvider root) : IRouteableModules, IDisposable
{
    // What AddGeneratedRoutes()/RegisterRoute put in a collection for their own use: the application's copy is the one that counts.
    private static readonly HashSet<Type> Infrastructure =
    [
        typeof(ScopedViewCache), typeof(WeakDisposableTracker), typeof(SingletonViewCache), typeof(RouteableTable), typeof(MenuEntries),
    ];

    private readonly object _gate = new();
    private readonly List<ModuleEntry> _loaded = [];
    private readonly Dictionary<object, (Func<IServiceProvider, IRouteableArgs, object> Factory, ModuleServices Services)> _factories = [];
    private readonly List<WeakReference<IMainViewModel>> _mains = [];

    private sealed record ModuleEntry(RouteableModuleInfo Info, IReadOnlyList<MenuEntries> Menus, ModuleServices Services, Action<IServiceCollection> Add);

    public event EventHandler<RouteableModuleInfo>? ModuleLoaded;

    public IReadOnlyList<RouteableModuleInfo> Loaded { get { lock (_gate) return _loaded.Select(l => l.Info).ToArray(); } }

    public bool IsLoaded(string name)
    {
        lock (_gate) return _loaded.Any(l => string.Equals(l.Info.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public RouteableModuleInfo Load(string name, Action<IServiceCollection> add)
        => Load(name, assembly: null, add);

    private RouteableModuleInfo Load(string name, string? assembly, Action<IServiceCollection> add)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(add);

        RouteableModuleInfo info;
        List<(IMainViewModel Main, IReadOnlyList<IRouteableEntry> Entries, MenuEntries Menu)> pending = [];
        lock (_gate)
        {
            var known = _loaded.FirstOrDefault(l => string.Equals(l.Info.Name, name, StringComparison.OrdinalIgnoreCase));
            if (known is not null)
            {
                // Loading a name again does nothing: a button clicked twice is not an error. Another way to add the same name is probably one:
                // the second module is not loaded, and the one that was is returned.
                if (assembly is null && !add.Equals(known.Add))
                    RouteableErrors.Report<RouteableModules>(root, new InvalidOperationException(
                        $"The module '{name}' is already loaded, with another method: this one was not run."));
                return known.Info;
            }

            // Everything that can fail runs before anything is changed: a module that throws, or whose entries cannot be built, is not
            // half loaded (it is not marked as loaded, and none of its routes or entries is there).
            var collection = new ServiceCollection();
            add(collection);

            var table = collection.Select(d => d.ImplementationInstance).OfType<RouteableTable>().FirstOrDefault();
            var routes = (table?.Entries.Select(e => e.Template) ?? []).ToArray();
            var menus = collection.Where(d => d.ServiceType == typeof(MenuEntries)).Select(d => (MenuEntries)d.ImplementationInstance!).ToArray();
            var services = new ModuleServices(collection.Where(d => !Infrastructure.Contains(d.ServiceType) && !IsRouteFactory(d)));

            foreach (var main in AliveMains())
                foreach (var menu in menus)
                    pending.Add((main, menu.Build().ToList(), menu));

            // The routes the router can reach from an address.
            if (table is not null && root.GetService<RouteableTable>() is { } applicationTable)
                applicationTable.AddRange(table);

            // The factories that build the views and the view models. The ones of the application come first.
            foreach (var d in collection.Where(IsRouteFactory))
                _factories.TryAdd(d.ServiceKey!, ((Func<IServiceProvider, IRouteableArgs, object>)d.KeyedImplementationInstance!, services));

            info = new RouteableModuleInfo(name, assembly, routes);
            _loaded.Add(new ModuleEntry(info, menus, services, add));
        }

        foreach (var (main, entries, menu) in pending)
            menu.AddTo(main, entries);

        ModuleLoaded?.Invoke(this, info);
        return info;
    }

    private static bool IsRouteFactory(ServiceDescriptor d)
        => d.IsKeyedService && d.ServiceType == typeof(Func<IServiceProvider, IRouteableArgs, object>) && d.KeyedImplementationInstance is not null;

    /// <summary>The factory a module registered for <paramref name="key"/>, and the provider to build it with.</summary>
    internal bool TryGetFactory(object key, IServiceProvider application,
        out Func<IServiceProvider, IRouteableArgs, object> factory, out IServiceProvider provider)
    {
        (Func<IServiceProvider, IRouteableArgs, object> Factory, ModuleServices Services) found;
        lock (_gate)
        {
            if (!_factories.TryGetValue(key, out found))
            {
                factory = null!;
                provider = null!;
                return false;
            }
        }

        factory = found.Factory;
        provider = new ModuleServiceProvider(application, found.Services);
        return true;
    }

    /// <summary>Adds the entries of the modules already loaded to a main view model, and keeps it to add the ones of the next.</summary>
    internal void Attach(IMainViewModel mainViewModel)
    {
        List<MenuEntries> menus;
        lock (_gate)
        {
            _mains.RemoveAll(m => !m.TryGetTarget(out _));
            _mains.Add(new WeakReference<IMainViewModel>(mainViewModel));
            menus = _loaded.SelectMany(l => l.Menus).ToList();
        }

        foreach (var menu in menus)
            menu.AddTo(mainViewModel);
    }

    private List<IMainViewModel> AliveMains()
    {
        _mains.RemoveAll(m => !m.TryGetTarget(out _));
        return _mains.Select(m => m.TryGetTarget(out var mainViewModel) ? mainViewModel : null).OfType<IMainViewModel>().ToList();
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Finds the methods of the module by reflection.")]
    public IReadOnlyList<RouteableModuleInfo> Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var found = new List<RouteableModuleInfo>();
        foreach (var type in assembly.GetExportedTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attribute = method.GetCustomAttribute<RouteableModuleAttribute>(inherit: false);
                if (attribute is null)
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(IServiceCollection))
                    throw new InvalidOperationException(
                        $"{type.FullName}.{method.Name} is marked [RouteableModule] but does not take one IServiceCollection.");

                var name = attribute.Name ?? method.Name;
                found.Add(Load(name, assembly.GetName().Name, services => method.Invoke(null, [services])));
            }
        }

        if (found.Count == 0)
            throw new InvalidOperationException($"{assembly.GetName().Name} has no public static method marked [RouteableModule].");

        return found;
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Loads an assembly and finds the methods of the module by reflection.")]
    public IReadOnlyList<RouteableModuleInfo> LoadFrom(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No module at '{path}'.", path);

        var context = new ModuleLoadContext(path);
        return Load(context.LoadFromAssemblyPath(Path.GetFullPath(path)));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var module in _loaded)
                module.Services.Dispose();
        }
    }
}
