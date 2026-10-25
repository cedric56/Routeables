using System.Collections.ObjectModel;

namespace Routeables.Abstraction;

/// <summary>
/// The main view model of an application: the menu entries, the one that is selected and the view it shows. Each host binds its
/// menu to <see cref="Routeables"/> and <see cref="Routeable"/>, and shows <see cref="CurrentView"/>. Register it with
/// <c>RegisterMainViewModel</c>: one instance per host (a singleton on Avalonia, MAUI and Uno, scoped on Blazor).
/// </summary>
public interface IMainViewModel
{
    /// <summary>The entries of the menu, in the order they are shown.</summary>
    ObservableCollection<IRouteableEntry> Routeables { get; }

    /// <summary>The selected entry. Setting it goes to that entry; <see langword="null"/> when none is selected.</summary>
    IRouteableEntry? Routeable { get; set; }

    /// <summary>The view the selected entry shows, or <see langword="null"/> when there is none.</summary>
    object? CurrentView { get; set; }

    /// <summary>The title of the selected entry (its display name), for a title bar.</summary>
    string? CurrentTitle { get; }

    /// <summary>Raised when <see cref="CurrentView"/> changes; the argument is the new view.</summary>
    EventHandler<object?>? CurrentViewChanged { get; set; }

    /// <summary>Raised after <see cref="Lock"/> or <see cref="Unlock"/> has changed the entries.</summary>
    EventHandler? LockChanged { get; set; }

    /// <summary>
    /// The router moved by something other than a selection of the menu (the Back request of the system): show the page that is
    /// now current and select its entry, without routing again. The base classes of Routeables.Core do it; the default does nothing.
    /// </summary>
    /// <param name="viewModel">The view model that is now current.</param>
    void OnRouterMoved(object? viewModel) { }

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
