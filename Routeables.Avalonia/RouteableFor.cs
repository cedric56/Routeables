using Avalonia;
using Avalonia.Controls;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Avalonia;

/// <summary>The base of the views of Routeables for Avalonia, a <see cref="UserControl"/>.</summary>
public abstract class RouteableFor : UserControl
{
    /// <summary>Whether every entry of the main view model is locked (a page is blocking the route change).</summary>
    public abstract bool IsLocked { get; }
}

/// <summary>A <see cref="RouteableFor"/> with its view model typed: the router gives it as the <c>DataContext</c>.</summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public class RouteableFor<TViewModel> : RouteableFor, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
{
    /// <summary>The view model of the view: the router gives it as the <c>DataContext</c>.</summary>
    public object? Context
    {
        get => DataContext;
        set => DataContext = value;
    }

    /// <summary>The view model, typed.</summary>
    public TViewModel ViewModel => (TViewModel)Context!;

    /// <summary>The <see cref="RouteableFor.IsLocked"/> property.</summary>
    public static readonly DirectProperty<RouteableFor, bool> IsLockedProperty =
    AvaloniaProperty.RegisterDirect<RouteableFor, bool>(
        nameof(IsLocked),
        o => o.IsLocked);

    /// <inheritdoc />
    public override bool IsLocked => RouteableLockWatcher.AllLocked(Context as IMainViewModel);

    /// <inheritdoc />
    public virtual Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        return Task.FromResult(result: true);
    }

    // Follows the lock of the main view model; one subscription, however many times the view is initialized.
    private RouteableLockWatcher? _lock;

    private RouteableLockWatcher Lock => _lock ??= new RouteableLockWatcher(_ => RaisePropertyChanged(IsLockedProperty, false, true));

    /// <inheritdoc />
    public void Dispose()
    {
        Unsubscribe();
        _lock?.Dispose();
    }

    private object? _subscribed;

    /// <summary>Called once for each view model the view shows, unlike <c>InitializeAsync</c> which runs again each time the view is shown.
    /// Register here what must be registered once (a messenger, an event).</summary>
    protected virtual void OnSubscribe() { }

    /// <summary>Called when the view is disposed or gets another view model: undo what <see cref="OnSubscribe"/> did.</summary>
    protected virtual void OnUnsubscribe() { }

    private void Subscribe()
    {
        var context = Context;
        if (context is null || ReferenceEquals(context, _subscribed))
            return;
        Unsubscribe();
        _subscribed = context;
        OnSubscribe();
    }

    private void Unsubscribe()
    {
        if (_subscribed is null)
            return;
        _subscribed = null;
        OnUnsubscribe();
    }

    /// <inheritdoc />
    public virtual Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        Subscribe();
        // Initialize runs again when a cached view is shown again: one subscription, not one more each time.
        Lock.Follow(Context);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
