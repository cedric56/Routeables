using Routeables.Abstraction;

namespace Routeables.Maui.Services;

/// <summary>
/// Shows a view as a modal over the current page and returns its result. <c>RouteableModalFor</c> calls it; replace it (the
/// CommunityToolkit Reshape does) to draw modals with another library.
/// </summary>
public interface IRouteableModalService
{
    /// <summary>Shows <paramref name="viewFactory"/>'s view as a modal and waits until <paramref name="viewModel"/> closes it.</summary>
    /// <typeparam name="TViewModel">The type of the view model, which asks to be closed.</typeparam>
    /// <typeparam name="TResult">The type of the value the modal returns.</typeparam>
    /// <param name="viewModel">The view model of the modal; its <see cref="ICloseableViewModel{TResult}.Commands"/> become the buttons.</param>
    /// <param name="viewFactory">Creates the content to show.</param>
    /// <returns>The value the view model closed with, or <see langword="default"/> when the modal was dismissed.</returns>
    Task<TResult?> ShowModalAsync<TViewModel, TResult>(
        TViewModel viewModel,
        Func<View> viewFactory)
        where TViewModel : ICloseableViewModel<TResult>;
}
