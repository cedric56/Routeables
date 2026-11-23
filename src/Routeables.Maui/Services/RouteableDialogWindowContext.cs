namespace Routeables.Maui.Services;

/// <summary>Finds the page that dialogs and modals are shown over: the page of the active window that has a handler, else of any window that has one, else the active one, else the first.</summary>
public sealed class RouteableDialogWindowContext : IRouteableDialogWindowContext
{
    /// <summary>The page to show a dialog over, or <see langword="null"/> when the application has no window yet.</summary>
    public Page? CurrentPage
    {
        get
        {
            // With several windows (Windows, Mac Catalyst) each has a handler: the active one is asked first, so a dialog is not always shown over the first.
            return Application.Current?.Windows.FirstOrDefault(w => w.IsActivated && w.Handler?.MauiContext != null)?.Page
            ?? Application.Current?.Windows.FirstOrDefault(w => w.Handler?.MauiContext != null)?.Page
            ?? Application.Current?.Windows.FirstOrDefault(w => w.IsActivated)?.Page
            ?? Application.Current?.Windows.FirstOrDefault()?.Page
            ?? Shell.Current?.CurrentPage;
        }
    }
}
