namespace Routeables.Abstraction.Lifecycle;

/// <summary>
/// Implemented by a view model (or a view) that can hold changes the user has not saved. <see cref="IRouteable.CanLeaveAsync"/> answers when the
/// router is asked to leave the page; this tells the host <b>before</b>, so that it can warn when the user closes the window or the browser tab
/// without going through the router. Blazor asks the browser to confirm (<c>beforeunload</c>) while <see cref="HasUnsavedChanges"/> is
/// <see langword="true"/>.
/// </summary>
/// <remarks>
/// The answer must be quick and synchronous: a browser reads it while the tab is closing. Raise <see cref="UnsavedChangesChanged"/> when it changes
/// (not on every keystroke if it stays <see langword="true"/>). The text of the warning is the browser's own: a page cannot set it.
/// </remarks>
public interface IRouteableUnsavedChanges
{
    /// <summary>Whether the page holds changes that would be lost.</summary>
    bool HasUnsavedChanges { get; }

    /// <summary>Raised when <see cref="HasUnsavedChanges"/> changes.</summary>
    event EventHandler? UnsavedChangesChanged;
}
