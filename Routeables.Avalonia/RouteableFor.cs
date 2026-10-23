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
    public object? Context
    {
        get => DataContext;
        set => DataContext = value;
    }

    public TViewModel ViewModel => (TViewModel)Context!;

    public static readonly DirectProperty<RouteableFor, bool> IsLockedProperty =
    AvaloniaProperty.RegisterDirect<RouteableFor, bool>(
        nameof(IsLocked),
        o => o.IsLocked);

    public override bool IsLocked => RouteableLockWatcher.AllLocked(Context as IMainRouteables);

    public virtual Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        return Task.FromResult(result: true);
    }

    // Follows the lock of the main view model; one subscription, however many times the view is initialized.
    private RouteableLockWatcher? _lock;

    private RouteableLockWatcher Lock => _lock ??= new RouteableLockWatcher(_ => RaisePropertyChanged(IsLockedProperty, false, true));

    public void Dispose() => _lock?.Dispose();

    public virtual Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        // Initialize runs again when a cached view is shown again: one subscription, not one more each time.
        Lock.Follow(Context);

        return Task.CompletedTask;
    }

    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
