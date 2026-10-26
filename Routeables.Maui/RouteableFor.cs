using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui;

/// <summary>
/// The base of the views of Routeables for MAUI. It is a <see cref="ContentView"/>: the router gives the view model to
/// <see cref="Context"/>, which is the <c>BindingContext</c>. A view that wants its view model typed derives from
/// <see cref="RouteableFor{TViewModel}"/> instead.
/// </summary>
public partial class RouteableFor : ContentView, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
{
    private static readonly BindablePropertyKey IsLockedPropertyKey =
        BindableProperty.CreateReadOnly(nameof(IsLocked), typeof(bool), typeof(RouteableFor), false);

    /// <summary>Whether every entry of the main view model is locked (a page is blocking the route change). Bindable.</summary>
    public static readonly BindableProperty IsLockedProperty = IsLockedPropertyKey.BindableProperty;

    // Follows the lock of the main view model; one subscription, however many times the view is initialized.
    private RouteableLockWatcher? _lock;

    private RouteableLockWatcher Lock => _lock ??= new RouteableLockWatcher(locked =>
    {
        // The lock changes on the thread that asked for the route change.
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(() => SetValue(IsLockedPropertyKey, locked));
        else
            SetValue(IsLockedPropertyKey, locked);
    });

    /// <summary>The view model: the <c>BindingContext</c> of the view.</summary>
    public object? Context
    {
        get => BindingContext;
        set => BindingContext = value;
    }

    /// <summary>Whether every entry of the main view model is locked. It only means something on the view of the main view model.</summary>
    public bool IsLocked => (bool)GetValue(IsLockedProperty);

    /// <inheritdoc />
    public virtual Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        return Task.FromResult(result: true);
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
        SetValue(IsLockedPropertyKey, Lock.Follow(Context));

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

    /// <summary>Stops following the lock of the main view model.</summary>
    public void Dispose()
    {
        Unsubscribe();
        _lock?.Dispose();
    }
}

/// <summary>A <see cref="RouteableFor"/> with its view model typed.</summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public class RouteableFor<TViewModel> : RouteableFor
{
    /// <summary>The view model the router gave to the view.</summary>
    public TViewModel ViewModel => (TViewModel)Context!;
}
