namespace Routeables.Maui.Services;

/// <summary>Finds the page that dialogs and modals are shown over: the page of the window that has a handler, else the active one, else the first.</summary>
public sealed class RouteableDialogWindowContext : IRouteableDialogWindowContext
{
    /// <summary>The page to show a dialog over, or <see langword="null"/> when the application has no window yet.</summary>
    public Page? CurrentPage
    {
        get
        {
            return Application.Current?.Windows.FirstOrDefault(w => w.Handler?.MauiContext != null)?.Page
            ?? Application.Current?.Windows.FirstOrDefault(w => w.IsActivated)?.Page
            ?? Application.Current?.Windows.FirstOrDefault()?.Page
            ?? Shell.Current?.CurrentPage;
        }
    }
}
