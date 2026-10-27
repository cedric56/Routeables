using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;

namespace Reshapes.Maui.CommunityToolkit;

/// <inheritdoc />
public class ReshapeTaskDialogService(IRouteableDialogWindowContext context) : IRouteableTaskDialogService
{
    /// <summary>
    /// Displays a task dialog assembled from header, subheader, content, progress bar, and footer
    /// sections, and awaits the user's button selection.
    /// </summary>
    /// <param name="params">The task dialog's configuration, including header, content, and footer.</param>
    /// <param name="results">Up to three result values corresponding to the primary, secondary, and close buttons.</param>
    /// <returns>The result value associated with the button the user selected.</returns>
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var popup = new TaskDialogPopup(@params, results);

        var page = context.CurrentPage
            ?? throw new InvalidOperationException("No active Page to host the task dialog.");

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
            var popupResult = await page.ShowPopupAsync<RouteableTaskDialogResult>(
                popup,
                PopupLook.Options());

            if (popupResult.WasDismissedByTappingOutsideOfPopup)
                return results.Length > 2 ? results[2] : RouteableTaskDialogResult.None;

            return popupResult.Result;
        }
        finally
        {
            popup.Session.Release();
        }
    }

    /// <summary>The task dialog (see <see cref="RouteableDialogContents.ForTaskDialog"/>) in a CommunityToolkit popup.</summary>
    public sealed class TaskDialogPopup : Popup<RouteableTaskDialogResult>
    {
        /// <inheritdoc />
        public TaskDialogPopup(RouteableTaskDialog p, RouteableTaskDialogResult[] results)
        {
            CanBeDismissedByTappingOutsideOfPopup = false;
            BackgroundColor = Colors.Transparent;

            // The dialog is closed with the result its session decided; without results it offers Close.
            Session = new RouteableTaskDialogSession(p, results, () => _ = CloseAsync(Session!.Result));
            Content = RouteableDialogContents.ForTaskDialog(p, Session);
        }

        /// <inheritdoc />
        public RouteableTaskDialogSession Session { get; }
    }
}
