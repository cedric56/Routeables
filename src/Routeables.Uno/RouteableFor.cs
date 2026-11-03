using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Uno;

/// <summary>
/// The base of the views of Routeables for Uno Platform. It is a <see cref="UserControl"/>, so it can be the root element of a XAML file:
/// <code>
/// &lt;routeables:RouteableFor
///     x:Class="MyApp.Views.OrderView"
///     xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
///     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
///     xmlns:routeables="using:Routeables.Uno"&gt;
///     ...
/// &lt;/routeables:RouteableFor&gt;
/// </code>
/// The router gives the view model to <see cref="Context"/>, which is the <c>DataContext</c>.
/// A view that wants its view model typed derives from <see cref="RouteableFor{TViewModel}"/> instead, in code.
/// </summary>
public partial class RouteableFor : UserControl, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
{
    /// <summary>Whether every entry of the main view model is locked (a page is blocking the route change). Bindable.</summary>
    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(RouteableFor), new PropertyMetadata(false));

    // Follows the lock of the main view model; one subscription, however many times the view is initialized.
    private RouteableLockWatcher? _lock;

    private RouteableLockWatcher Lock => _lock ??= new RouteableLockWatcher(locked =>
    {
        // The lock changes on the thread that asked for the route change.
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => SetValue(IsLockedProperty, locked));
        else
            SetValue(IsLockedProperty, locked);
    });

    /// <summary>The view model: the <c>DataContext</c> of the view.</summary>
#if ANDROID
    // A native Android view already has a Context (the Android one): this one is the view model.
    public new object? Context
#else
    public object? Context
#endif
    {
        get => DataContext;
        set => DataContext = value;
    }

    /// <summary>Whether every entry of the main view model is locked. It only means something on the view of the main view model.</summary>
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
        SetValue(IsLockedProperty, Lock.Follow(Context));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>Stops following the lock of the main view model.</summary>
    // Uno's DependencyObject already has a Dispose(): this one hides it (hence new), does its own work, then lets the base do the rest.
    public new void Dispose()
    {
        Unsubscribe();
        _lock?.Dispose();
#if !ANDROID && !IOS
        // Not on a native platform: disposing a native control by hand can crash (Uno0002); its owner releases it.
        base.Dispose();
#endif
    }
}

/// <summary>A <see cref="RouteableFor"/> with its view model typed, for a view written in code.</summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public partial class RouteableFor<TViewModel> : RouteableFor
{
    /// <summary>The view model the router gave to the view.</summary>
    public TViewModel ViewModel => (TViewModel)Context!;
}