using Routeables.Abstraction;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>A module in the list of the Modules page, with the command that opens its page.</summary>
public sealed class ModuleItem(ModuleInfo info, ICommand open)
{
    public string Name => info.Name;
    public string Description => info.Description;
    public string Route => info.Route;
    public ICommand OpenCommand => open;

    /// <summary>The text of the button, the same on every platform.</summary>
    public string OpenText => $"Open {info.Name}";
}

/// <summary>
/// The Modules page: the modules the application loaded (the <see cref="ModuleInfo"/> each <c>Add...Module()</c> registers) and a
/// button that opens the page of each one. A module is loaded at start, in <c>services</c>; there is nothing to load here.
/// </summary>
public class ModulesViewModel : ViewModelBase
{
    private readonly IMainRouteables _main;

    public ModulesViewModel(IMainRouteables main, IEnumerable<ModuleInfo> modules)
    {
        _main = main;
        Modules = modules.Select(module => new ModuleItem(module, new AsyncCommand(() => OpenAsync(module.Route)))).ToList();
    }

    public IReadOnlyList<ModuleItem> Modules { get; }

    public bool HasModules => Modules.Count > 0;

    public bool HasNoModules => Modules.Count == 0;

    /// <summary>Selects the entry of the module in the menu: the main view model shows its page.</summary>
    public Task OpenAsync(string route)
    {
        if (_main.Routeables.FirstOrDefault(entry => entry.Route == route) is { } entry)
            _main.Routeable = entry;

        return Task.CompletedTask;
    }
}
