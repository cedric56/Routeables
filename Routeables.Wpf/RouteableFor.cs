using System.Windows;
using System.Windows.Controls;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Wpf;

/// <summary>
/// Base class of a routed WPF view. Derive a <see cref="UserControl"/> from it in XAML (<c>&lt;routeables:RouteableFor ...&gt;</c>).
/// The view model is its <see cref="FrameworkElement.DataContext"/>; <see cref="IsLocked"/> follows the lock of the main view model.
/// </summary>
public class RouteableFor : UserControl, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
{
    private static readonly DependencyPropertyKey IsLockedKey =
        DependencyProperty.RegisterReadOnly(nameof(IsLocked), typeof(bool), typeof(RouteableFor), new PropertyMetadata(false));

    /// <summary>Identifies <see cref="IsLocked"/>, usable in a binding.</summary>
    public static readonly DependencyProperty IsLockedProperty = IsLockedKey.DependencyProperty;

    // Follows the lock of the main view model; one subscription, however many times the view is initialized.
    private RouteableLockWatcher? _lock;

    private RouteableLockWatcher Lock => _lock ??= new RouteableLockWatcher(locked =>
    {
        // The lock changes on the thread that asked for the route change.
        if (Dispatcher.CheckAccess())
            SetValue(IsLockedKey, locked);
        else
            Dispatcher.InvokeAsync(() => SetValue(IsLockedKey, locked));
    });

    /// <summary>Gets or sets the view model, as an <see cref="object"/> (the <see cref="FrameworkElement.DataContext"/>).</summary>
    public object? Context
    {
        get => DataContext;
        set => DataContext = value;
    }

    /// <summary>Gets whether the application is locked: a route change is running or a page refused to be left.</summary>
    public bool IsLocked => (bool)GetValue(IsLockedProperty);

    /// <inheritdoc />
    public virtual Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);

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
        SetValue(IsLockedKey, Lock.Follow(Context));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        Unsubscribe();
        _lock?.Dispose();
    }
}

/// <summary>A routed view whose view model has a known type.</summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public class RouteableFor<TViewModel> : RouteableFor
{
    /// <summary>Gets the view model.</summary>
    public TViewModel ViewModel => (TViewModel)Context!;
}
