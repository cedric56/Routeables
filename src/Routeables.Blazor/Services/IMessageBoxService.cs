using Routeables.Abstraction.Bases;
using Routeables.Services;

namespace Routeables.Blazor.Services;

/// <summary>
/// Where the dialogs of the Razor host are drawn. <c>DefaultMessageBoxService</c> draws them itself, with the hosts of the layout; a Reshape (such as
/// <c>ReshapeMessageBoxService</c> for MudBlazor) replaces it to draw them with a UI library. Pass yours to
/// <c>services.UseRouteablesServices(messageBoxService: ...)</c>.
/// </summary>
public interface IMessageBoxService : IRouteableMessageBoxBase
{
    /// <summary>Shows the modal of <paramref name="viewModel"/> and waits until it closes it. The Razor default throws: a modal is shown by its component (<c>RouteableModalFor</c>).</summary>
    /// <typeparam name="TViewModel">The type of the view model, which asks to be closed.</typeparam>
    /// <typeparam name="TResult">The type of the value the modal returns.</typeparam>
    /// <param name="viewModel">The view model of the modal; its <see cref="ICloseableViewModel{TResult}.Commands"/> become the buttons.</param>
    /// <returns>The value the view model closed with, or <see langword="default"/> when the modal was dismissed.</returns>
    Task<TResult?> ShowModalAsync<TViewModel, TResult>(
        TViewModel viewModel)
        where TViewModel : ICloseableViewModel<TResult>;
}
