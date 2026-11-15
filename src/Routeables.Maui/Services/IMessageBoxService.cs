using Routeables.Abstraction.Bases;
using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// Where the dialogs of the MAUI host are drawn. <see cref="RouteableDialogService"/> draws them with the native dialogs; a Reshape (such as
/// <c>ReshapeMessageBoxService</c> for CommunityToolkit) replaces it to draw them with a UI library. Pass yours to
/// <c>MauiAppBuilder.UseRouteablesServices(messageBoxService: ...)</c>.
/// </summary>
public interface IMessageBoxService : IRouteableMessageBoxBase
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
