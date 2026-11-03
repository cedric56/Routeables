namespace Routeables.Abstraction;

/// <summary>
/// A page that gives a value back to whoever opened it: the view model (or the view) of a page opened with
/// <c>router.GoToForResultAsync&lt;TResult&gt;(...)</c>. The router asks for the value when the page is left.
/// <code>
/// public sealed class ColorPickerViewModel(IRouter router) : IReturnsResult&lt;Color&gt;
/// {
///     private Color? _picked;
///     public Task PickAsync(Color color) { _picked = color; return router.PopAsync(); }
///     public bool TryGetResult(out Color result) { result = _picked.GetValueOrDefault(); return _picked is not null; }
/// }
/// </code>
/// </summary>
/// <typeparam name="TResult">The type of the value given back.</typeparam>
public interface IReturnsResult<TResult>
{
    /// <summary>
    /// Gives the value the page returns. Called once, when the page is left; return <see langword="false"/> when the user left it
    /// without choosing (Back, a cancelled dialog): the caller then gets <see cref="RouteableResult{TResult}.Returned"/> false.
    /// </summary>
    bool TryGetResult(out TResult result);
}
