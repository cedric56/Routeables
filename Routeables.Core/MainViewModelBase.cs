using Routeables.Abstraction;
using System.ComponentModel;

namespace Routeables.Core;

/// <summary>
/// A main view model that works as it is: derive from it, return the entries of the menu, and register it with
/// <c>services.RegisterMainViewModel&lt;T&gt;()</c>. It does not implement <see cref="INotifyPropertyChanged"/>: the class that derives from it does (see the remarks).
/// <code>
/// public sealed class MainViewModel(IRouter router) : MainViewModelBase(router)
/// {
///     protected override ObservableCollection&lt;IRouteableEntry&gt; GetRouteables() =>
///     [
///         new RouteableEntry&lt;CatsViewModel&gt;("Cats", route: "/cats", template: "/cats"),
///         new RouteableEntry&lt;DogsViewModel&gt;("Dogs", route: "/dogs", template: "/dogs"),
///     ];
/// }
/// </code>
/// </summary>
/// <remarks>
/// <b>This class does not implement <see cref="INotifyPropertyChanged"/>: a class derived from it must.</b> Put <c>[INotifyPropertyChanged]</c>
/// (or <c>[ObservableObject]</c>) of CommunityToolkit.Mvvm or <c>[IReactiveObject]</c> of ReactiveUI on your <c>partial</c> class, or implement the
/// interface by hand and raise the event from <see cref="RouteableViewModelBase.NotifyPropertyChanged(string?)"/>:
/// <code>
/// public sealed class MainViewModel(IRouter router) : MainViewModelBase(router), INotifyPropertyChanged
/// {
///     public event PropertyChangedEventHandler? PropertyChanged;
///     protected override void NotifyPropertyChanged(string? propertyName)
///         => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
/// }
/// </code>
/// <para>
/// <see cref="RouteableViewModelBase.GetRouteables"/> runs while the base class is being constructed, before the fields of
/// the derived class exist: build the entries from the code alone (as above), not from a field or a constructor parameter.
/// </para>
/// </remarks>
/// <param name="router">The router that shows the selected entry.</param>
/// <param name="initialize">Whether the first entry is selected when the view model is created.</param>
public abstract class MainViewModelBase(IRouter router, bool initialize = true) : RouteableViewModelBase(router, initialize)
{
    /// <summary>Nothing to refresh: this class has no commands. Override it to refresh yours.</summary>
    protected override void RaiseCanExecutesChanged()
    {
    }
}
