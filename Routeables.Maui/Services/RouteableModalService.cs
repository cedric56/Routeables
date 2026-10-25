using Routeables.Abstraction;

namespace Routeables.Maui.Services;

/// <summary>Shows a modal page for a view model that can be closed with a result (<c>ICloseableViewModel</c>) above the current page of the window.</summary>
/// <param name="context">Where the current page of the window is found.</param>
public class RouteableModalService(IRouteableDialogWindowContext context) : IRouteableModalService
{
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

        return await popup.ShowAsync(page);
    }

    private sealed class ModalPopup<TResult> : RouteablePopupPage<TResult>
    {
        private readonly ICloseableViewModel<TResult> _viewModel;

        public ModalPopup(ICloseableViewModel<TResult> viewModel, View view)
        {
            _viewModel = viewModel;
            _viewModel.CloseRequested += OnCloseRequested;

            // Dressed like the platform's own dialog; the first command is the primary button.
            Dialog = RouteableDialogContents.ForModal(viewModel, view);
        }

        private async void OnCloseRequested(object? sender, TResult? result)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            try { await CloseAsync(result); }
            catch (Exception ex) { RouteableErrors.Report(null, ex, typeof(RouteableModalService)); }   // async void: an exception would end the application
        }

        // The page went away by other means than the view model closing it: stop listening to the view model.
        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            base.OnHandlerChanging(args);
            if (args.NewHandler is null)
                _viewModel.CloseRequested -= OnCloseRequested;
        }
    }
}
