using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Core;

// The selection of an entry: one at a time, the newest wins, and a refused or failed one puts the previous entry back.
public abstract partial class RouteableMenuViewModelBase
{
    private IRouteableMenuEntry? _routeable;

    // The entry whose page the router has shown. A selection replaced by a newer one before it started never showed its page, so it is not the
    // entry to go back to when the newer selection is refused.
    private IRouteableMenuEntry? _confirmed;
    // Selections run one at a time, and a newer one replaces the ones before it.
    private readonly SelectionQueue _selections = new();

    // The token of the selection that runs: the gate serializes the selections, so it is the one the router is given.
    private CancellationToken _selectionToken;

    /// <summary>
    /// The selected entry. Setting it selects that entry without waiting (use <see cref="SelectAsync(IRouteableMenuEntry?, IRouteableMenuEntry?)"/> to wait for the result).
    /// </summary>
    public IRouteableMenuEntry? SelectedEntry
    {
        get => _routeable;
        set
        {
            if (Equals(_routeable, value) && !PageIsElsewhere(value))
                return;
            var old = _routeable;
            _routeable = value;

            // Fire-and-forget, for XAML two-way binding (a setter cannot be async). SelectAsync below does the real work, serialized and
            // version-guarded. Callers that need to await completion or observe failure should call SelectAsync directly.
            _ = ObserveAsync(SelectAsync(value, old), value);
        }
    }

    // The entry is selected but the router shows another page: a page opened from code (a detail, say) has no entry, and the menu keeps the
    // one it was on. Selecting that entry again must open its page, not be ignored.
    private bool PageIsElsewhere(IRouteableMenuEntry? entry)
        => entry is not null && _selections.Version != 0 && _router.History.Current is { } current
           && !RouteAddress.SameFor(_router, current.Args.Route, entry.Arguments.Route ?? entry.Route);

    /// <summary>
    /// Selects an entry like the <see cref="SelectedEntry"/> setter does, and waits for the result.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the entry is selected once its page is shown; <see langword="false"/> when a guard refused it or its route failed
    /// (the menu is then back on the entry it was on).
    /// </returns>
    /// <param name="value">The entry to select.</param>
    public Task<bool> SelectEntryAsync(IRouteableMenuEntry? value) => SelectEntryAsync(value, CancellationToken.None);

    /// <summary>
    /// Selects an entry like the <see cref="SelectedEntry"/> setter does, and waits for the result.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the entry is selected once its page is shown; <see langword="false"/> when a guard refused it, its route failed
    /// or the selection was cancelled (the menu is then back on the entry it was on).
    /// </returns>
    /// <param name="value">The entry to select.</param>
    /// <param name="cancellationToken">Cancels the selection, and the route change that it asks of the router (a navigation that the browser gave up).</param>
    public async Task<bool> SelectEntryAsync(IRouteableMenuEntry? value, CancellationToken cancellationToken)
    {
        if (Equals(_routeable, value) && !PageIsElsewhere(value))
            return true;
        var old = _routeable;
        _routeable = value;
        var selection = SelectAsync(value, old, cancellationToken);
        await ObserveAsync(selection, value);
        // A cancelled selection did not select anything, whatever the menu shows, so it answers "no".
        return !selection.IsCanceled && ReferenceEquals(_routeable, value);
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
        catch (OperationCanceledException)
        {
            // A cancelled selection (the navigation that asked for it was abandoned) is not a failure to report.
        }
        catch (Exception exception)
        {
            OnSelectionFailed(exception);
        }

        // When a guard answers immediately, the control that selected the entry is still inside the selection and ignores a notification raised
        // there, so it would keep showing the refused entry. Announce the selected entry after the setter returns, on the same context.
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
    public Task SelectAsync(IRouteableMenuEntry? value, IRouteableMenuEntry? old = null) => SelectAsync(value, old, CancellationToken.None);

    /// <summary>
    /// Selects an entry and waits for its view to be shown. Selections run one at a time; a selection that a newer one has replaced does nothing.
    /// </summary>
    /// <param name="value">The entry to select.</param>
    /// <param name="old">The previous entry, restored on failure.</param>
    /// <param name="cancellationToken">Cancels the selection while it waits for another one, and the route change that it asks of the router.</param>
    public async Task SelectAsync(IRouteableMenuEntry? value, IRouteableMenuEntry? old, CancellationToken cancellationToken)
    {
        var version = _selections.Begin();

        // No ConfigureAwait(false): the notifications go to the UI, so the continuation must stay on the UI context.
        try
        {
            await _selections.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancelled while waiting for another selection, so it never ran. If it is still the newest, the menu goes back to its previous entry
            // and the selection holding the gate becomes the newest again (or it would think it was replaced and leave the menu on the wrong entry).
            // A newer selection owns the state: nothing to do then.
            if (_selections.Abandon(version))
            {
                _routeable = old;
                NotifyPropertyChanged(nameof(SelectedEntry));
                NotifyPropertyChanged(nameof(CurrentTitle));
            }

            throw;
        }

        try
        {
            // A newer selection was requested while we waited for the gate, so our work is already stale before it starts: do not touch any state.
            if (version != _selections.Version)
                return;

            _selectionToken = cancellationToken;
            await OnRouteableChangedAsync(value, old, version);
        }
        finally
        {
            _selectionToken = default;
            _selections.Finish(version);
        }
    }

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

    // The entry to select again when a selection is not shown: the one whose page the router shows. The `old` of a selection replaced before it
    // started was never shown, so it is the answer only when no page was shown through this menu yet.
    private IRouteableMenuEntry? Previous(IRouteableMenuEntry? old, int version) => version != 0 ? _confirmed ?? old : old;

    /// <summary>
    /// Shows the view of an entry through the router and announces it with <see cref="CurrentViewChanged"/>; the previous entry is selected again if that fails.
    /// </summary>
    /// <param name="value">The entry to show, or <see langword="null"/> for none.</param>
    /// <param name="old">The previously selected entry, restored on failure.</param>
    /// <param name="version">
    /// The selection version this call is running under, as assigned by <see cref="SelectAsync(IRouteableMenuEntry?, IRouteableMenuEntry?, CancellationToken)"/>.
    /// </param>
    public virtual async Task OnRouteableChangedAsync(IRouteableMenuEntry? value, IRouteableMenuEntry? old, int version = 0)
    {
        try
        {
            if (value == null)
                return;

            RouteableResult outcome;
            Interlocked.Increment(ref _selecting);
            try { outcome = await _router.GoToRouteAsync(value, _selectionToken); }
            finally { Interlocked.Decrement(ref _selecting); }
            var result = outcome.Success;

            // Whatever a newer selection does next, the router now shows this entry: it is the one to go back to if that selection is refused.
            if (result && _router.History.Current is { } shown
                && RouteAddress.SameFor(_router, shown.Args.Route, value.Arguments.Route ?? value.Route))
                _confirmed = value;

            // A newer selection replaced this one while the router was working: its result is the one to keep.
            if (version != 0 && version != _selections.Version)
                return;

            if (result)
            {
                // A guard sent the user to another page (a sign-in): the menu does not claim the refused entry, unless the page shown has an entry
                // of its own (OnViewModelChanged selected it).
                var redirected = _router.History.Current is { } current
                    && !RouteAddress.SameFor(_router, current.Args.Route, value.Arguments.Route ?? value.Route);
                // The redirection is a Push that arrived while the selection ran, and OnRouted left it out: the menu follows that page now.
                if (redirected && !Follows(_router.CurrentViewModel))
                    OnRouterMoved(_router.CurrentViewModel);
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
            if (version == 0 || version == _selections.Version)
                _routeable = Previous(old, version);

            throw;
        }
        finally
        {
            if (version == 0 || version == _selections.Version)
            {
                RaiseCanExecutesChanged();
                NotifyPropertyChanged(nameof(SelectedEntry));
            }
        }
    }
}
