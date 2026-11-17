using System.Collections.ObjectModel;

namespace Routeables.Abstraction.Bases;

/// <summary>
/// The menu view model of an application: the menu entries, the one that is selected and the view it shows. Each host binds its
/// menu to <see cref="Entries"/> and <see cref="SelectedEntry"/>; the page itself is shown by a <c>RouteableRegion</c> (the view is <c>IRouter.CurrentView</c>). Register it with
/// <c>RegisterMenuViewModel</c>: one instance per host (a singleton on Avalonia, MAUI and Uno, scoped on Blazor).
/// </summary>
public interface IRouteableMenuViewModel
{
    /// <summary>The entries of the menu, in the order they are shown.</summary>
    ObservableCollection<IRouteableMenuEntry> Entries { get; }

    /// <summary>The selected entry. Setting it goes to that entry; <see langword="null"/> when none is selected.</summary>
    IRouteableMenuEntry? SelectedEntry { get; set; }

    /// <summary>The title of the selected entry (its display name), for a title bar. Override it for another title.</summary>
    string? CurrentTitle => SelectedEntry?.DisplayName;

    /// <summary>Raised when the page shown or the selected entry changes; the argument is the view that is now shown (<c>IRouter.CurrentView</c>).</summary>
    event EventHandler<object?>? CurrentViewChanged;

    /// <summary>Raised after <see cref="Lock"/> or <see cref="Unlock"/> has changed the entries.</summary>
    event EventHandler? LockChanged;

    /// <summary>
    /// Raises <see cref="LockChanged"/>. An interface cannot raise its own event, so <see cref="Lock"/> and <see cref="Unlock"/> call this:
    /// a class that implements the interface by hand raises the event here (the base classes of Routeables.Core do it); the default does nothing.
    /// </summary>
    void NotifyLockChanged() { }

    /// <summary>
    /// The router moved by something other than a selection of the menu (the Back request of the system): show the page that is
    /// now current and select its entry, without routing again. The base classes of Routeables.Core do it; the default does nothing.
    /// </summary>
    /// <param name="viewModel">The view model that is now current.</param>
    void OnRouterMoved(object? viewModel) { }

    /// <summary>
    /// Selects an entry and says whether it is the selected one afterwards: <see langword="false"/> when a guard or a newer route change kept the
    /// user on the page they were on. The address bar of Blazor asks it for an address typed or followed, and puts the address of the page back when
    /// the answer is <see langword="false"/>. The default sets <see cref="SelectedEntry"/> and answers <see langword="true"/>, since it cannot tell
    /// what the setter of a menu written by hand did; a menu that can (it awaits the route change) implements this method itself. The base classes of Routeables.Core do.
    /// </summary>
    /// <param name="entry">The entry to select.</param>
    /// <param name="cancellationToken">Cancels the selection (a navigation that the browser gave up).</param>
    /// <returns><see langword="true"/> when <paramref name="entry"/> is selected.</returns>
    Task<bool> SelectEntryAsync(IRouteableMenuEntry entry, CancellationToken cancellationToken = default)
    {
        SelectedEntry = entry;
        return Task.FromResult(true);
    }

    /// <summary>
    /// Selects the first entry when nothing is selected yet. Every host calls it once at start, so the first page is chosen
    /// the same way on Blazor, Avalonia and MAUI.
    /// </summary>
    /// <returns><see langword="true"/> when an entry was selected.</returns>
    bool SelectFirst()
    {
        if (SelectedEntry is not null || Entries.FirstOrDefault() is not { } first)
            return false;

        SelectedEntry = first;
        return true;
    }

    /// <summary>
    /// Opens the home route of an application that has no menu. The default region (a <c>RouteableRegion</c> with no name) calls it
    /// when it appears, so the routing starts where the pages are shown; <see cref="SelectFirst"/> stays for the applications that
    /// have a menu. It does nothing when a page is already shown, and the same instance starts only once.
    /// </summary>
    /// <returns><see langword="true"/> when the home route is being opened.</returns>
    bool Start() => false;

    /// <summary>Locks every entry (<see cref="IRouteableMenuEntry.IsLock"/>): the menu cannot leave the current page. Raises <see cref="LockChanged"/> through <see cref="NotifyLockChanged"/>.</summary>
    void Lock()
    {
        foreach (var routeable in Entries)
            routeable.IsLock = true;

        NotifyLockChanged();
    }

    /// <summary>Unlocks every entry again. Raises <see cref="LockChanged"/> through <see cref="NotifyLockChanged"/>.</summary>
    void Unlock()
    {
        foreach (var routeable in Entries)
            routeable.IsLock = false;

        NotifyLockChanged();
    }
}
