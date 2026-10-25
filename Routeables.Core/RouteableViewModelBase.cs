using Routeables.Abstraction;
using Routeables.Routing;
using System.Collections.ObjectModel;

namespace Routeables.Core;

/// <summary>
/// The base of a main view model (the menu or tab bar): it lists <see cref="IRouteableEntry"/> entries, shows the view of the selected
/// one through the router, and keeps the entry selected when a page of the router changes.
/// </summary>
/// <remarks>
/// <b>This class does not implement <see cref="System.ComponentModel.INotifyPropertyChanged"/>: a class derived from it must.</b>
/// The generators of the MVVM libraries add the interface to the class they are applied to, and only once in a hierarchy, so it is
/// left to the derived class: put <c>[INotifyPropertyChanged]</c> (or <c>[ObservableObject]</c>) of CommunityToolkit.Mvvm, or
/// <c>[IReactiveObject]</c> of ReactiveUI, on the <c>partial</c> class, or implement the interface by hand, then override
/// <see cref="NotifyPropertyChanged(string?)"/> to raise the event and <see cref="RouterViewModelBase.RaiseCanExecutesChanged"/> to refresh
/// the commands. <c>RouteableViewModel</c> and <c>CloseableViewModel&lt;TResult&gt;</c> (the CommunityToolkit.Mvvm and ReactiveUI flavours) do it already.
/// </remarks>
public abstract partial class RouteableViewModelBase : RouterViewModelBase, IDisposable, IRouteable, IMainViewModel
{
    /// <summary>
    /// Occurs when the currently displayed view changes.
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

        // The entry of that view model: its route is the type name, or it is a RouteableEntry<T> of that type.
        var type = viewModel.GetType();
        var entry = Routeables.FirstOrDefault(p => p.Route == type.Name || (p as RouteableEntry)?.ViewModelType == type);
        if (entry != null)
        {
            _routeable = entry;
            _currentView = entry.View;
        }
        NotifyPropertyChanged(nameof(Routeable));
        NotifyPropertyChanged(nameof(CurrentView));
        NotifyPropertyChanged(nameof(CurrentTitle));
        CurrentViewChanged?.Invoke(this, _currentView);
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

        // A page with no entry of its own in the menu (OnViewModelChanged found none) is shown all the same.
        var view = _router.CurrentView;
        if (view is not null && !ReferenceEquals(_currentView, view))
            CurrentView = view;
    }

    /// <summary>
    /// Raises a property-changed notification for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected abstract void NotifyPropertyChanged(string? propertyName);

    private object? _currentView;

    /// <summary>
    /// The view being displayed. Setting it raises <see cref="CurrentViewChanged"/> and a property-changed notification.
    /// </summary>
    public object? CurrentView
    {
        get { return _currentView; }
        set
        {
            _currentView = value;
            NotifyPropertyChanged(nameof(CurrentView));
            NotifyPropertyChanged(nameof(CurrentTitle));
            CurrentViewChanged?.Invoke(this, _currentView);
        }
    }

    private IRouteableEntry? _routeable;
    private readonly SemaphoreSlim _selectionGate = new(1, 1);
    private int _selectionVersion;

    /// <summary>
    /// The selected entry. Setting it selects that entry without waiting (use <see cref="SelectAsync"/> to wait for the result).
    /// </summary>
    public IRouteableEntry? Routeable
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
            _ = ObserveAsync(SelectAsync(value, old));
        }
    }

    private ILockCoordinator? _coordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableViewModelBase"/> class.
    /// </summary>
    /// <param name="router">The router that shows the views.</param>
    /// <param name="initialize">
    /// If <see langword="true"/>, the first entry of <see cref="Routeables"/> is selected on construction.
    /// </param>
    public RouteableViewModelBase(IRouter router, bool initialize = true)
        : base(router)
    {
        _coordinator = router.CreateLockCoordinator(this);
        _coordinator?.Subscribe();
        _router.Routed += OnRouted;

        if (initialize)
        {
            _routeable = Routeables.FirstOrDefault();
            _ = ObserveAsync(SelectAsync(_routeable, null));
        }
    }

    /// <summary>
    /// Observes an asynchronous task, catching and reporting any unhandled exceptions via <see cref="OnSelectionFailed"/>.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            OnSelectionFailed(exception);
        }
    }

    /// <summary>
    /// Called when a selection started by the <see cref="Routeable"/> setter (or the constructor) fails.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    protected virtual void OnSelectionFailed(Exception exception)
        => RouteableErrors.Report<RouteableViewModelBase>(null, exception);

    /// <summary>
    /// Selects an entry and waits for its view to be shown. Selections run one at a time; a selection that a newer one has replaced does nothing.
    /// </summary>
    /// <param name="value">The entry to select.</param>
    /// <param name="old">The previous entry, restored on failure.</param>
    public async Task SelectAsync(IRouteableEntry? value, IRouteableEntry? old = null)
    {
        var version = Interlocked.Increment(ref _selectionVersion);

        // No ConfigureAwait(false): CurrentView raises UI notifications, so the continuation must stay on the UI context.
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

    private ObservableCollection<IRouteableEntry>? _routeables;

    /// <summary>
    /// The entries of the menu, built by <see cref="GetRouteables"/> on first access.
    /// </summary>
    public ObservableCollection<IRouteableEntry> Routeables { get { return _routeables ??= GetRouteables(); } }

    /// <inheritdoc />
    public virtual string? CurrentTitle { get => Routeable?.DisplayName ?? "Application"; }

    /// <summary>
    /// Returns the entries of the menu.
    /// </summary>
    /// <returns>The entries.</returns>
    protected abstract ObservableCollection<IRouteableEntry> GetRouteables();

    /// <summary>
    /// Shows the view of an entry through the router and updates <see cref="CurrentView"/>; the previous entry is selected again if that fails.
    /// </summary>
    /// <param name="value">The entry to show, or <see langword="null"/> for none.</param>
    /// <param name="old">The previously selected entry, restored on failure.</param>
    /// <param name="version">
    /// The selection version this call is running under, as assigned by <see cref="SelectAsync"/>.
    /// </param>
    public virtual async Task OnRouteableChangedAsync(IRouteableEntry? value, IRouteableEntry? old, int version = 0)
    {
        try
        {
            if (value == null)
                return;

            var result = (await _router.GoToRouteAsync(value)).Success;

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
                    _routeable = old;

                CurrentView = _router.CurrentView!;
            }
            else
            {
                _routeable = old;
            }
        }
        catch
        {
            if (version == 0 || version == _selectionVersion)
                _routeable = old;

            throw;
        }
        finally
        {
            if (version == 0 || version == _selectionVersion)
            {
                RaiseCanExecutesChanged();
                NotifyPropertyChanged(nameof(Routeable));
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