using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui.Services;

internal class RouteableTaskDialogService(IRouteableDialogWindowContext context) : IRouteableTaskDialogService
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

        var page = context.CurrentPage
            ?? throw new InvalidOperationException("No active Page to host the task dialog.");

        var popup = new TaskDialogPopup(@params, results);
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

    private sealed class TaskDialogPopup : RouteablePopupPage<RouteableTaskDialogResult>
    {
        public TaskDialogPopup(RouteableTaskDialog p, RouteableTaskDialogResult[] results)
        {
            // The dialog is closed with the result its session decided; without results it offers Close.
            Session = new RouteableTaskDialogSession(p, results, () => _ = CloseAsync(Session!.Result));
            Dialog = RouteableDialogContents.ForTaskDialog(p, Session);
        }

        public RouteableTaskDialogSession Session { get; }
    }
}
