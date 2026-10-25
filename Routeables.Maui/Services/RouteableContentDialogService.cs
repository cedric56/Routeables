using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui.Services;

internal class RouteableContentDialogService(IRouteableDialogWindowContext context) : IRouteableContentDialogService
{
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var page = context.CurrentPage
            ?? throw new InvalidOperationException(
                "No active Page to host the content dialog popup.");

        var popup = new ContentDialogPopup(@params);

        // The popup closes itself on the main thread, whichever thread asks.
        popup.Session.AllowCloseFromCode(close => close());
        popup.FirstAppearing += (_, _) => popup.Session.Opened();

        popup.Session.Opening();
        try
        {
            return await popup.ShowAsync(page);
        }
        finally
        {
            popup.Session.Release();
        }
    }

    private sealed class ContentDialogPopup : RouteablePopupPage<RouteableContentDialogResult>
    {
        public ContentDialogPopup(RouteableContentDialog p)
        {
            // The dialog is closed with the result its session decided.
            Session = new RouteableContentDialogSession(p, () => _ = CloseAsync(Session!.Result));
            Dialog = RouteableDialogContents.ForContentDialog(p, Session);
        }

        public RouteableContentDialogSession Session { get; }
    }
}
