using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Reshapes.Maui.CommunityToolkit;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;

namespace Reshapes.Maui.Community;

internal class ReshapeMessageBoxService(IRouteableDialogWindowContext context) : IMessageBoxService
{
    /// <inheritdoc />
    public async Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
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

    /// <inheritdoc />
    public async Task<TResult?> ShowModalAsync<TViewModel, TResult>(
    TViewModel viewModel,
    Func<View> viewFactory)
    where TViewModel : ICloseableViewModel<TResult>
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(viewFactory);

        var page = context.CurrentPage
            ?? throw new InvalidOperationException(
                "No active Page to host the modal popup.");

        var view = viewFactory()
            ?? throw new InvalidOperationException(
                "The modal view factory returned null.");

        var popup = new ModalPopup<TResult>(viewModel, view);

        try
        {
            var result = await page.ShowPopupAsync<TResult?>(
                popup,
                PopupLook.Options());

            return result.WasDismissedByTappingOutsideOfPopup
                ? default
                : result.Result;
        }
        finally
        {
            // Dismissed by a tap outside, the view model never asked to close: stop listening to it.
            popup.Detach();
        }
    }

    sealed class ModalPopup<TResult> : Popup<TResult?>
    {
        private readonly ICloseableViewModel<TResult> _viewModel;
        private bool _closed;

        public ModalPopup(
            ICloseableViewModel<TResult> viewModel,
            View view)
        {
            _viewModel = viewModel;

            _viewModel.CloseRequested += OnCloseRequested;

            BackgroundColor = Colors.Transparent;

            // Dressed like the platform's own dialog; the first command is the primary button.
            Content = RouteableDialogContents.ForModal(viewModel, view);
        }

        private async void OnCloseRequested(
       object? sender,
       TResult? result)
        {
            try { await CloseOnceAsync(result); }
            catch (Exception ex) { RouteableErrors.Report(null, ex, typeof(ReshapeMessageBoxService)); }   // async void
        }

        /// <summary>Stops listening to the view model.</summary>
        public void Detach() => _viewModel.CloseRequested -= OnCloseRequested;

        private async Task CloseOnceAsync(TResult? result)
        {
            if (_closed)
                return;

            _closed = true;
            _viewModel.CloseRequested -= OnCloseRequested;

            await CloseAsync(result);
        }
    }

    /// <summary>
    /// Displays a task dialog assembled from header, subheader, content, progress bar, and footer
    /// sections, and awaits the user's button selection.
    /// </summary>
    /// <param name="params">The task dialog's configuration, including header, content, and footer.</param>
    /// <param name="results">Up to three result values corresponding to the primary, secondary, and close buttons.</param>
    /// <returns>The result value associated with the button the user selected.</returns>
    public async Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
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
