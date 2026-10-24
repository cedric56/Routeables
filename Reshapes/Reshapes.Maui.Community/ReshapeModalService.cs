using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Routeables.Abstraction;
using Routeables.Maui.Services;

namespace Reshapes.Maui.CommunityToolkit;

public class ReshapeModalService(IRouteableDialogWindowContext context) : IRouteableModalService
{
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
            catch (Exception ex) { RouteableErrors.Report(null, ex, typeof(ReshapeModalService)); }   // async void
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
}