// Uno is what is left when no other host is named: the Routeables.Uno project links this file without defining a symbol.
#if !WPF && !AVALONIA && !MAUI && !WINUI3
#define ROUTEABLE_UNO
#endif
#if WPF
using System.Windows;
using System.Windows.Controls;
#elif AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
#elif MAUI
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#endif
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Services;

// One file for every host: Routeables.Wpf, Routeables.Avalonia and Routeables.Maui link it (and define WPF, AVALONIA or MAUI),
// Routeables.Uno and Routeables.WinUI3 link it too.
#if WPF
namespace Routeables.Wpf;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif MAUI
namespace Routeables.Maui;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

#if WPF
/// <summary>
/// Base class of a routed WPF view. Derive a <see cref="UserControl"/> from it in XAML (<c>&lt;routeables:RouteableFor ...&gt;</c>).
/// The view model is its <see cref="FrameworkElement.DataContext"/>; <see cref="IsLocked"/> follows the lock of the menu view model.
/// </summary>
public partial class RouteableFor : UserControl, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
#elif AVALONIA
/// <summary>
/// The base of the views of Routeables for Avalonia, a <see cref="UserControl"/>. The router gives the view model as the
/// <c>DataContext</c>; a view that wants it typed derives from <see cref="RouteableFor{TViewModel}"/>.
/// </summary>
public abstract partial class RouteableFor : UserControl, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
#elif MAUI
/// <summary>
/// The base of the views of Routeables for MAUI. It is a <see cref="ContentView"/>: the router gives the view model to
/// <see cref="Context"/>, which is the <c>BindingContext</c>. A view that wants its view model typed derives from
/// <see cref="RouteableFor{TViewModel}"/> instead.
/// </summary>
public partial class RouteableFor : ContentView, IRouteableFor, IRouteable, IDisposable, IInitializeAsync
#else
/// <summary>
/// The base of the views of Routeables for Uno Platform and WinUI 3. It is a <see cref="UserControl"/>, so it can be the root element of a XAML file:
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
#endif
{
#if WPF
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableForAutomationPeer</c>).</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableForAutomationPeer(this);

    private static readonly DependencyPropertyKey IsLockedKey =
        DependencyProperty.RegisterReadOnly(nameof(IsLocked), typeof(bool), typeof(RouteableFor), new PropertyMetadata(false));

    /// <summary>Identifies <see cref="IsLocked"/>, usable in a binding.</summary>
    public static readonly DependencyProperty IsLockedProperty = IsLockedKey.DependencyProperty;

    private void SetLocked(bool locked) => SetValue(IsLockedKey, locked);

    // The lock changes on the thread that asked for the route change.
    private void OnInterfaceThread(Action action)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.InvokeAsync(action);
    }
#elif AVALONIA
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableForAutomationPeer</c>).</summary>
    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableForAutomationPeer(this);

    /// <summary>The <see cref="IsLocked"/> property.</summary>
    public static readonly DirectProperty<RouteableFor, bool> IsLockedProperty =
        AvaloniaProperty.RegisterDirect<RouteableFor, bool>(nameof(IsLocked), o => o.IsLocked);

    // IsLocked is computed from the view model: the value only tells the bindings to read it again.
    private void SetLocked(bool locked) => RaisePropertyChanged(IsLockedProperty, false, true);

    // The lock changes on the thread that asked for the route change.
    private static void OnInterfaceThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
#elif MAUI
    private static readonly BindablePropertyKey IsLockedPropertyKey =
        BindableProperty.CreateReadOnly(nameof(IsLocked), typeof(bool), typeof(RouteableFor), false);

    /// <summary>Whether every entry of the menu view model is locked (a page is blocking the route change). Bindable.</summary>
    public static readonly BindableProperty IsLockedProperty = IsLockedPropertyKey.BindableProperty;

    private void SetLocked(bool locked) => SetValue(IsLockedPropertyKey, locked);

    // The lock changes on the thread that asked for the route change.
    private void OnInterfaceThread(Action action)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(action);
        else
            action();
    }
#else
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableForAutomationPeer</c>).</summary>
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableForAutomationPeer(this);

    /// <summary>Whether every entry of the menu view model is locked (a page is blocking the route change). Bindable.</summary>
    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(RouteableFor), new PropertyMetadata(false));

    private void SetLocked(bool locked) => SetValue(IsLockedProperty, locked);

    // The lock changes on the thread that asked for the route change.
    private void OnInterfaceThread(Action action)
    {
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => action());
        else
            action();
    }
#endif

    // Follows the lock of the menu view model; one subscription, however many times the view is initialized.
    private RouteableLockWatcher? _lock;

    private RouteableLockWatcher Lock => _lock ??= new RouteableLockWatcher(locked => OnInterfaceThread(() => SetLocked(locked)));

#if MAUI
    /// <summary>The view model: the <c>BindingContext</c> of the view.</summary>
#elif AVALONIA
    /// <summary>The view model of the view: the router gives it as the <c>DataContext</c>.</summary>
#elif WPF
    /// <summary>Gets or sets the view model, as an <see cref="object"/> (the <see cref="FrameworkElement.DataContext"/>).</summary>
#else
    /// <summary>The view model: the <c>DataContext</c> of the view.</summary>
#endif
#if ANDROID && ROUTEABLE_UNO
    // A native Android view already has a Context (the Android one): this one is the view model.
    public new object? Context
#else
    public object? Context
#endif
    {
#if MAUI
        get => BindingContext;
        set => BindingContext = value;
#else
        get => DataContext;
        set => DataContext = value;
#endif
    }

#if AVALONIA
    /// <summary>Whether every entry of the menu view model is locked (a page is blocking the route change).</summary>
    public bool IsLocked => RouteableLockWatcher.AllLocked(Context as IRouteableMenuViewModel);
#elif WPF
    /// <summary>Gets whether the application is locked: a route change is running or a page refused to be left.</summary>
    public bool IsLocked => (bool)GetValue(IsLockedProperty);
#else
    /// <summary>Whether every entry of the menu view model is locked. It only means something on the view of the menu view model.</summary>
    public bool IsLocked => (bool)GetValue(IsLockedProperty);
#endif

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
#if AVALONIA
        Lock.Follow(Context);
#else
        SetLocked(Lock.Follow(Context));
#endif

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;

#if ROUTEABLE_UNO
    /// <summary>Stops following the lock of the menu view model.</summary>
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
#else
    /// <summary>Stops following the lock of the menu view model.</summary>
    public void Dispose()
    {
        Unsubscribe();
        _lock?.Dispose();
    }
#endif
}

/// <summary>A <see cref="RouteableFor"/> with its view model typed: the router gives it as the context of the view.</summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public partial class RouteableFor<TViewModel> : RouteableFor
{
    /// <summary>The view model the router gave to the view.</summary>
    public TViewModel ViewModel => (TViewModel)Context!;
}
