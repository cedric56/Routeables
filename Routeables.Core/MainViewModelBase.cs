using Routeables.Abstraction;
using System.ComponentModel;

namespace Routeables.Core;

/// <summary>
/// A main view model that works as it is: derive from it, return the entries of the menu, and register it with
/// <c>services.RegisterMainRouteables&lt;T&gt;()</c>. It raises <see cref="INotifyPropertyChanged"/> itself, so it needs no MVVM library.
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
/// <see cref="RouteableViewModelBase.GetRouteables"/> runs while the base class is being constructed, before the fields of
/// the derived class exist: build the entries from the code alone (as above), not from a field or a constructor parameter.
/// </remarks>
/// <param name="router">The router that shows the selected entry.</param>
/// <param name="initialize">Whether the first entry is selected when the view model is created.</param>
public abstract class MainViewModelBase(IRouter router, bool initialize = true) : RouteableViewModelBase(router, initialize), INotifyPropertyChanged
{
    /// <summary>Raised when a property changes (<see cref="RouteableViewModelBase.Routeable"/>, <see cref="RouteableViewModelBase.CurrentView"/>, <see cref="Title"/>).</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The name of the selected entry, for a host that shows a title; "Application" before any is selected.</summary>
    public virtual string Title => Routeable?.DisplayName ?? "Application";

    /// <inheritdoc />
    protected override void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName == nameof(Routeable))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
    }

    /// <summary>Nothing to refresh: this class has no commands. Override it to refresh yours.</summary>
    protected override void RaiseCanExecutesChanged()
    {
    }
}
