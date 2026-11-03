namespace Routeables.Maui.Services;

/// <summary>
/// Tells the dialog and modal services which window they draw on: MAUI has one scope per window, so the page that is on screen
/// is asked rather than a global one.
/// </summary>
public interface IRouteableDialogWindowContext
{
    /// <summary>The page currently shown in the window, or <see langword="null"/> when there is none.</summary>
    Page? CurrentPage { get; }
}
