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
    /// Follows the router: when a page is shown by something other than a selection, selects its entry
    /// and shows its view.
    /// </summary>
    /// <param name="viewModel">The view model that is now current.</param>
    protected override void OnViewModelChanged(object? viewModel)
    {
        if (viewModel == null)
            return;

        _followed = viewModel;

        // The entry of that view model: its route is the type name, or it is a RouteableMenuEntry<T> of that type.
        var type = viewModel.GetType();
        var entry = Entries.FirstOrDefault(p => p.Route == type.Name || (p as RouteableMenuEntry)?.ViewModelType == type);
        if (entry != null)
            _routeable = _confirmed = entry;
        NotifyPropertyChanged(nameof(SelectedEntry));
        NotifyPropertyChanged(nameof(CurrentTitle));
        CurrentViewChanged?.Invoke(this, _router.CurrentView);
        base.OnViewModelChanged(viewModel);
    }

    // A page that moves the router itself (its own Back or Pop button, for instance) does not tell the menu: the router does.
    // A selection of the menu, and a move that the menu has already followed, are not told twice.
    private object? _followed;

    // Set while GoBackAsync and GoForwardAsync of this menu run: they follow the router themselves, once the move is done.
    private bool _selfMoving;

    /// <inheritdoc />
    public override async Task GoBackAsync()
    {
        _selfMoving = true;
        try { await base.GoBackAsync(); }
        finally { _selfMoving = false; }
    }

    /// <inheritdoc />
    public override async Task GoForwardAsync()
    {
        _selfMoving = true;
        try { await base.GoForwardAsync(); }
        finally { _selfMoving = false; }
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        if (_selfMoving || !e.Succeeded || e.Kind == RouteableKind.Push || ReferenceEquals(_router.CurrentViewModel, _followed))
            return;

        OnRouterMoved(_router.CurrentViewModel);
    }

    /// <inheritdoc />
    public void OnRouterMoved(object? viewModel)
    {
        _followed = viewModel;
        OnViewModelChanged(viewModel);
    }

    /// <summary>
    /// Raises a property-changed notification for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected abstract void NotifyPropertyChanged(string? propertyName);

    private IRouteableMenuEntry? _routeable;

    // The entry whose page the router has shown. A selection that a newer one replaced before it started never showed its page, so it is
    // not the entry to go back to when the newer selection is refused.
    private IRouteableMenuEntry? _confirmed;
    private readonly SemaphoreSlim _selectionGate = new(1, 1);
    private int _selectionVersion;

    /// <summary>
    /// The selected entry. Setting it selects that entry without waiting (use <see cref="SelectAsync"/> to wait for the result).
    /// </summary>
    public IRouteableMenuEntry? SelectedEntry
    {
        get => _routeable;
        set
        {
            if (Equals(_routeable, value))
                return;
            var old = _routeable;
            _routeable = value;

            // Fire-and-forget for XAML two-way binding (the setter itself can't be async).
            // SelectAsync below does the actual serialized, version-guarded work; callers that
            // need to await completion or observe failure should call SelectAsync directly
            // instead of going through this property.
            _ = ObserveAsync(SelectAsync(value, old), value);
        }
    }

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

    /// <summary>
    /// Observes an asynchronous task, catching and reporting any unhandled exceptions via <see cref="OnSelectionFailed"/>.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="requested">
    /// The entry the selection asked for. When the menu does not end on it (a guard such as <c>CanLeaveAsync</c> refused, or the route
    /// failed), the selected entry is announced again once the setter has returned.
    /// </param>
    private async Task ObserveAsync(Task task, IRouteableMenuEntry? requested = null)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            OnSelectionFailed(exception);
        }

        // The control that selected the entry is still inside the selected entry when a guard answers at once, and it ignores
        // a notification raised there: it would keep showing the refused entry as selected. Announce the entry that is selected
        // after the setter has returned, on the same context.
        if (requested is not null && !ReferenceEquals(_routeable, requested))
        {
            await Task.Yield();
            NotifyPropertyChanged(nameof(SelectedEntry));
            NotifyPropertyChanged(nameof(CurrentTitle));
        }
    }

    /// <summary>
    /// Called when a selection started by the <see cref="SelectedEntry"/> setter (or the constructor) fails.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    protected virtual void OnSelectionFailed(Exception exception)
        => RouteableErrors.Report<RouteableMenuViewModelBase>(null, exception);

    /// <summary>
    /// Selects an entry and waits for its view to be shown. Selections run one at a time; a selection that a newer one has replaced does nothing.
    /// </summary>
    /// <param name="value">The entry to select.</param>
    /// <param name="old">The previous entry, restored on failure.</param>
    public async Task SelectAsync(IRouteableMenuEntry? value, IRouteableMenuEntry? old = null)
    {
        var version = Interlocked.Increment(ref _selectionVersion);

        // No ConfigureAwait(false): the notifications go to the UI, so the continuation must stay on the UI context.
        await _selectionGate.WaitAsync();
        try
        {
            // A newer selection was requested while we were waiting for the gate — our work
            // is already stale before it even started, so don't touch any state.
            if (version != _selectionVersion)
                return;

            await OnRouteableChangedAsync(value, old, version);
        }
        finally
        {
            _selectionGate.Release();
        }
    }

    private ObservableCollection<IRouteableMenuEntry>? _routeables;

    /// <summary>
    /// The entries of the menu, built by <see cref="GetEntries"/> on first access.
    /// </summary>
    public ObservableCollection<IRouteableMenuEntry> Entries { get { return _routeables ??= GetEntries(); } }

    /// <inheritdoc />
    public virtual string? CurrentTitle { get => SelectedEntry?.DisplayName ?? "Application"; }

    /// <summary>
    /// Returns the entries of the menu.
    /// </summary>
    /// <returns>The entries.</returns>
    protected abstract ObservableCollection<IRouteableMenuEntry> GetEntries();

    /// <summary>
    /// Called when the router did not show the entry (a guard refused it, the route was cancelled, or an exception was thrown):
    /// the previous entry is selected again. Override it to log or display the reason; put a breakpoint here to see why.
    /// The default writes the reason to the debug output.
    /// </summary>
    /// <param name="entry">The entry that was not shown.</param>
    /// <param name="result">The outcome of the router: <see cref="RouteableResult.Cancelled"/> and <see cref="RouteableResult.Exception"/>.</param>
    protected virtual void OnRoutingFailed(IRouteableMenuEntry entry, RouteableResult result)
        => System.Diagnostics.Debug.WriteLine(
            $"Routeables: '{entry.Route}' was not shown ({(result.Cancelled ? "cancelled by a guard (CanLeaveAsync / CanEnter) or a newer route" : "failed")}){(result.Exception is null ? "" : ": " + result.Exception)}");

    // The entry to select again when a selection is not shown: the one whose page the router shows. The `old` of a selection that a newer
    // one replaced before it started was never shown, so it is only the answer when no page was shown through this menu yet.
    private IRouteableMenuEntry? Previous(IRouteableMenuEntry? old, int version) => version != 0 ? _confirmed ?? old : old;

    /// <summary>
    /// Shows the view of an entry through the router and announces it with <see cref="CurrentViewChanged"/>; the previous entry is selected again if that fails.
    /// </summary>
    /// <param name="value">The entry to show, or <see langword="null"/> for none.</param>
    /// <param name="old">The previously selected entry, restored on failure.</param>
    /// <param name="version">
    /// The selection version this call is running under, as assigned by <see cref="SelectAsync"/>.
    /// </param>
    public virtual async Task OnRouteableChangedAsync(IRouteableMenuEntry? value, IRouteableMenuEntry? old, int version = 0)
    {
        try
        {
            if (value == null)
                return;

            var outcome = await _router.GoToRouteAsync(value);
            var result = outcome.Success;

            // Whatever a newer selection does next, the router now shows this entry: it is the one to go back to if that one is refused.
            if (result && _router.History.Current is { } shown
                && string.Equals(shown.Args.Route, value.Arguments.Route ?? value.Route, StringComparison.Ordinal))
                _confirmed = value;

            // A newer selection replaced this one while the router was working: its result is the one to keep.
            if (version != 0 && version != _selectionVersion)
                return;

            if (result)
            {
                // A guard sent the user to another page (a sign-in): the menu does not claim the entry that was refused,
                // unless the page shown has an entry of its own (OnViewModelChanged selected it).
                var redirected = _router.History.Current is { } current
                    && !string.Equals(current.Args.Route, value.Arguments.Route ?? value.Route, StringComparison.Ordinal);
                if (redirected && ReferenceEquals(_routeable, value))
                    _routeable = Previous(old, version);

                NotifyPropertyChanged(nameof(CurrentTitle));
                CurrentViewChanged?.Invoke(this, _router.CurrentView);
            }
            else
            {
                _routeable = Previous(old, version);
                OnRoutingFailed(value, outcome);
            }
        }
        catch
        {
            if (version == 0 || version == _selectionVersion)
                _routeable = Previous(old, version);

            throw;
        }
        finally
        {
            if (version == 0 || version == _selectionVersion)
            {
                RaiseCanExecutesChanged();
                NotifyPropertyChanged(nameof(SelectedEntry));
            }
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public virtual void Dispose()
    {
        // The broker keeps the coordinator, and the coordinator this view model, for as long as the broker lives.
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