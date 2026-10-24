namespace Routeables.Maui.Services;

public sealed class RouteableDialogWindowContext : IRouteableDialogWindowContext
{
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
