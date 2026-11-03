namespace Routeables.Abstraction;

/// <summary>
/// A view that is shown as a modal and gives a result back: the base types of each host implement it, with a view model that
/// implements <see cref="ICloseableViewModel{TResult}"/>.
/// </summary>
/// <typeparam name="T">The type of the view model.</typeparam>
/// <typeparam name="TResult">The type of the value the modal returns.</typeparam>
public interface IRouteableModalFor<T, TResult> : IRouteableFor where T : class
{
    /// <summary>Shows the view as a modal and waits until it is closed.</summary>
    /// <returns>The value the view model closed with, or <see langword="default"/> when the modal was dismissed.</returns>
    Task<TResult?> ShowModalAsync();
}