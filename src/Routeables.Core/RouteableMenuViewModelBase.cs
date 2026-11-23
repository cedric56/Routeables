using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Routeables.Core;

/// <summary>
/// A menu view model that works as it is: derive from it, return the entries of the menu, and register it with
/// <c>services.RegisterMenuViewModel&lt;T&gt;()</c>. It does not implement <see cref="INotifyPropertyChanged"/>: the class that derives from it does (see the remarks).
/// <code>
/// public sealed class MainViewModel(IRouter router) : RouteableMenuViewModelBase(router)
/// {
///     protected override ObservableCollection&lt;IRouteableMenuEntry&gt; GetEntries() =>
///     [
///         new RouteableMenuEntry&lt;CatsViewModel&gt;("Cats", route: "/cats", template: "/cats"),
///         new RouteableMenuEntry&lt;DogsViewModel&gt;("Dogs", route: "/dogs", template: "/dogs"),
///     ];
/// }
/// </code>
/// </summary>
/// <remarks>
/// <b>This class does not implement <see cref="INotifyPropertyChanged"/>: a class derived from it must.</b> Put <c>[INotifyPropertyChanged]</c>
/// (or <c>[ObservableObject]</c>) of CommunityToolkit.Mvvm or <c>[IReactiveObject]</c> of ReactiveUI on your <c>partial</c> class, or implement the
/// interface by hand and raise the event from <see cref="RouteableMenuViewModelBase.NotifyPropertyChanged(string?)"/>:
/// <code>
/// public sealed class MainViewModel(IRouter router) : RouteableMenuViewModelBase(router), INotifyPropertyChanged
/// {
///     public event PropertyChangedEventHandler? PropertyChanged;
///     protected override void NotifyPropertyChanged(string? propertyName)
///         => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
/// }
/// </code>
/// <para>
/// <see cref="RouteableMenuViewModelBase.GetEntries"/> runs while the base class is being constructed, before the fields of
/// the derived class exist: build the entries from the code alone (as above), not from a field or a constructor parameter.
/// To build them from something your class holds, pass <c>initialize: false</c> to the base constructor: the entries are then built on their
/// first read, after the construction, and the host selects the first one when it starts (or call <see cref="IRouteableMenuViewModel.SelectFirst"/>).
/// </para>
/// </remarks>
public abstract partial class RouteableMenuViewModelBase : RouterViewModelBase, IDisposable, IRouteable, IRouteableMenuViewModel
{
    /// <summary>Nothing to refresh: a menu view model has no commands. Override it to refresh yours.</summary>
    protected override void RaiseCanExecutesChanged()
    {
    }

    /// <summary>
    /// Occurs when the page shown or the selected entry changes. The argument is the view that is now shown (<c>IRouter.CurrentView</c>).
    /// </summary>
    public event EventHandler<object?>? CurrentViewChanged;

    /// <summary>
    /// Occurs when the lock state of the entries changes.
    /// </summary>
    public event EventHandler? LockChanged;

    /// <inheritdoc />
    public void NotifyLockChanged() => LockChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Raises a property-changed notification for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected abstract void NotifyPropertyChanged(string? propertyName);

    private IRouteableLockCoordinator? _coordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableMenuViewModelBase"/> class.
    /// </summary>
    /// <param name="router">The router that shows the views.</param>
    /// <param name="initialize">
    /// If <see langword="true"/>, the first entry of <see cref="Entries"/> is selected on construction.
    /// </param>
    public RouteableMenuViewModelBase(IRouter router, bool initialize = true)
        : base(router)
    {
        _coordinator = router.CreateLockCoordinator(this);
        _coordinator?.Subscribe();
        _router.Routed += OnRouted;

        if (initialize)
        {
            _routeable = Entries.FirstOrDefault();
            _ = ObserveAsync(SelectAsync(_routeable, null));
        }
    }

    private ObservableCollection<IRouteableMenuEntry>? _routeables;

    /// <summary>
    /// The entries of the menu, built by <see cref="GetEntries"/> on first access.
    /// </summary>
    public ObservableCollection<IRouteableMenuEntry> Entries { get { return _routeables ??= BuildEntries(); } }

    private ObservableCollection<IRouteableMenuEntry> BuildEntries()
    {
        try
        {
            return GetEntries();
        }
        catch (Exception exception) when (exception is NullReferenceException or ArgumentNullException)
        {
            // The usual cause: GetEntries() reads a field or constructor parameter of the derived class while the base class is still being constructed.
            throw new InvalidOperationException(
                $"{GetType().Name}.GetEntries() threw a {exception.GetType().Name}. It runs while the base class is being constructed, before the fields and the constructor "
                + "parameters of the derived class are set: build the entries from the code alone, or pass initialize: false to the base constructor "
                + "(the entries are then built on their first read, after the construction).", exception);
        }
    }

    /// <inheritdoc />
    public virtual string? CurrentTitle { get => SelectedEntry?.DisplayName ?? RouteableTexts.ApplicationTitle; }

    /// <summary>
    /// Returns the entries of the menu.
    /// </summary>
    /// <returns>The entries.</returns>
    protected abstract ObservableCollection<IRouteableMenuEntry> GetEntries();

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public virtual void Dispose()
    {
        // The broker keeps the coordinator, and the coordinator keeps this view model, for as long as the broker lives.
        _coordinator?.Unsubscribe();
        _coordinator = null;
        _router.Routed -= OnRouted;

        _routeables?.Clear();
        _routeables = null;
    }

    /// <summary>
    /// Whether the menu may be left; always <see langword="true"/> here.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/>.</returns>
    public virtual Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Called when the menu is entered; does nothing here.
    /// </summary>
    /// <param name="args">The arguments of the route change.</param>
    /// <param name="ct">A cancellation token.</param>
    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the menu is left; does nothing here.
    /// </summary>
    /// <param name="args">The arguments of the route change.</param>
    /// <param name="ct">A cancellation token.</param>
    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}