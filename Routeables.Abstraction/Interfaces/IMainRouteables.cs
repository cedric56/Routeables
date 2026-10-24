using System.Collections.ObjectModel;

namespace Routeables.Abstraction;

/// <summary>
/// The main view model of an application: the menu entries, the one that is selected and the view it shows. Each host binds its
/// menu to <see cref="Routeables"/> and <see cref="Routeable"/>, and shows <see cref="CurrentView"/>. Register it with
/// <c>RegisterMainRouteables</c>: one instance per host (a singleton on Avalonia and MAUI, scoped on Blazor Server).
/// </summary>
public interface IMainRouteables
{
    /// <summary>The entries of the menu, in the order they are shown.</summary>
    ObservableCollection<IRouteableEntry> Routeables { get; }

    /// <summary>The selected entry. Setting it goes to that entry; <see langword="null"/> when none is selected.</summary>
    IRouteableEntry? Routeable { get; set; }

    /// <summary>The view the selected entry shows, or <see langword="null"/> when there is none.</summary>
    object? CurrentView { get; set; }

    string? CurrentTitle { get; }

    /// <summary>Raised when <see cref="CurrentView"/> changes; the argument is the new view.</summary>
    EventHandler<object?>? CurrentViewChanged { get; set; }

    /// <summary>Raised after <see cref="Lock"/> or <see cref="Unlock"/> has changed the entries.</summary>
    EventHandler? LockChanged { get; set; }

    /// <summary>
    /// Selects the first entry when nothing is selected yet. Every host calls it once at start, so the first page is chosen
    /// the same way on Blazor, Avalonia and MAUI.
    /// </summary>
    /// <returns><see langword="true"/> when an entry was selected.</returns>
    bool SelectFirst()
    {
        if (Routeable is not null || Routeables.FirstOrDefault() is not { } first)
            return false;

        Routeable = first;
        return true;
    }

    /// <summary>Locks every entry (<see cref="IRouteableEntry.IsLock"/>): the menu cannot leave the current page. Raises <see cref="LockChanged"/>.</summary>
    void Lock()
    {
        foreach (var routeable in Routeables)
            routeable.IsLock = true;

        LockChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unlocks every entry again. Raises <see cref="LockChanged"/>.</summary>
    void Unlock()
    {
        foreach (var routeable in Routeables)
            routeable.IsLock = false;

        LockChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// A page that hosts a view of its own, such as a list that shows the detail of the animal it opened. It follows the same pattern as
/// <see cref="IMainRouteables"/>, for one page instead of the whole menu.
/// </summary>
public interface IViewRouteables
{
    /// <summary>The hosted view, or <see langword="null"/> when none is shown.</summary>
    object? CurrentView { get; set; }

    /// <summary>Raised when <see cref="CurrentView"/> changes; the argument is the new view.</summary>
    EventHandler<object?>? CurrentViewChanged { get; set; }
}
