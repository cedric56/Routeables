using Routeables.Abstraction.Bases;

namespace Routeables.Abstraction.Lifecycle;

/// <summary>
/// Gives a view that does not implement <see cref="IRouteableFor"/> its view model: each host registers one that knows how its views keep
/// their data (the <c>DataContext</c> of an Avalonia, WPF or Uno control, the <c>BindingContext</c> of a MAUI element). The router asks it
/// for a view that is not an <see cref="IRouteableFor"/> itself; a view that is one is bound as before.
/// </summary>
public interface IRouteableViewBinder
{
    /// <summary>The way to read and write the view model of <paramref name="view"/>.</summary>
    /// <param name="view">The view the router shows.</param>
    /// <returns>An accessor of the view's context, or <see langword="null"/> when the view has none (the router then leaves it alone).</returns>
    IRouteableFor? Adapt(object view);
}
