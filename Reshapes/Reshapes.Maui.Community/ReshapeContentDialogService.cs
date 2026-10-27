using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;

namespace Reshapes.Maui.CommunityToolkit;

/// <inheritdoc />
public class ReshapeContentDialogService(IRouteableDialogWindowContext context) : IRouteableContentDialogService
{
    /// <inheritdoc />
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var popup = new ContentDialogPopup(@params);

        var page = context.CurrentPage
            ?? throw new InvalidOperationException(
                "No active Page to host the content dialog popup.");

        // The popup closes on the dispatcher of its page, whichever thread asks.
        popup.Session.AllowCloseFromCode(close => page.Dispatcher.Dispatch(close));
        var opened = false;
        popup.Loaded += (_, _) =>
        {
            if (opened) return;
            opened = true;
            popup.Session.Opened();
        };

        popup.Session.Opening();
        try
        {
            var result = await page.ShowPopupAsync<RouteableContentDialogResult>(
                popup,
                PopupLook.Options());

            return result.WasDismissedByTappingOutsideOfPopup
                ? RouteableContentDialogResult.None
                : result.Result;
        }
        finally
        {
            popup.Session.Release();
        }
    }

    /// <inheritdoc />

    public sealed class ContentDialogPopup : Popup<RouteableContentDialogResult>
    {
        /// <inheritdoc />
        public ContentDialogPopup(RouteableContentDialog p)
        {
            CanBeDismissedByTappingOutsideOfPopup = false;
            BackgroundColor = Colors.Transparent;

            // The dialog is closed with the result its session decided.
            Session = new RouteableContentDialogSession(p, () => _ = CloseAsync(Session!.Result));
            Content = RouteableDialogContents.ForContentDialog(p, Session);
        }

        /// <inheritdoc />
        public RouteableContentDialogSession Session { get; }
    }
}
