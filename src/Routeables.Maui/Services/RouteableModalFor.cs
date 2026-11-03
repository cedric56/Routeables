using Routeables.Abstraction;

namespace Routeables.Maui.Services;

/// <summary>
/// Base view for a modal backed by a closeable view model. The view is shown in a popup by the
/// <see cref="IMessageBoxService"/>; the buttons come from the view model's <see cref="ICloseableViewModel{TResult}.Commands"/>.
/// </summary>
/// <typeparam name="TViewModel">The closeable view model type driving this modal.</typeparam>
/// <typeparam name="TResult">The result type produced when the modal closes.</typeparam>
public class RouteableModalFor<TViewModel, TResult> : RouteableFor, IRouteableModalFor<TViewModel, TResult>
    where TViewModel : class, ICloseableViewModel<TResult>
{
    /// <summary>
    /// Shows this view modally and awaits the value the view model passes to <see cref="ICloseableViewModel{TResult}.CloseAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The view has no view model, or no modal service is available.</exception>
    public Task<TResult?> ShowModalAsync()
    {
        var viewModel = BindingContext as TViewModel
            ?? throw new InvalidOperationException($"The context of {GetType().Name} is not a {typeof(TViewModel).Name}.");

        var services = Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("No service provider is available to show the modal.");

        return services.GetRequiredService<IMessageBoxService>().ShowModalAsync<TViewModel, TResult>(viewModel, () => this);
    }
}
