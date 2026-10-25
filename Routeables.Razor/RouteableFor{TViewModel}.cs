using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Routeables.Abstraction;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Razor;

/// <summary>
/// A generic Razor component base class that pairs a specific Blazor component view (<typeparamref name="TView"/>) 
/// with a view model (<typeparamref name="TViewModel"/>), implementing routing lifecycle interfaces, 
/// property change subscriptions, and dynamic rendering.
/// </summary>
/// <typeparam name="TView">The Blazor component type representing the view.</typeparam>
/// <typeparam name="TViewModel">The view model type implementing.</typeparam>
public partial class RouteableFor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TView, TViewModel> :
    RouteableFor,
    IRouteable,
    IInitializeAsync,
    IDisposable
    where TViewModel : class
    where TView : ComponentBase
{
    /// <summary>
    /// Gets a value indicating whether all nested routeable elements are currently locked.
    /// </summary>
    public bool IsLocked
    {
        get => ViewModel is IMainRouteables routeables &&
            routeables.Routeables != null &&
            routeables.Routeables.Count > 0 &&
            routeables.Routeables.All(r => r.IsLock);
    }

    /// <summary>
    /// Gets the strongly typed view model instance cast from the base <see cref="RouteableFor.Context"/>.
    /// </summary>
    public TViewModel ViewModel => (TViewModel)Context!;

    /// <summary>
    /// Determines whether routing away from this view is currently permitted.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A task that completes with a boolean value indicating whether routing can proceed.</returns>
    public virtual Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        return Task.FromResult(result: true);
    }

    /// <summary>
    /// Initializes the view model or view asynchronously with routing arguments.
    /// </summary>
    /// <param name="args">The route change arguments.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    public virtual Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called asynchronously when routing is leaving this view.
    /// </summary>
    /// <param name="args">The route change arguments.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    public virtual Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called asynchronously when routing is entering this view.
    /// </summary>
    /// <param name="args">The route change arguments.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    public virtual Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    private TViewModel? _subscribed;

    /// <summary>
    /// Manages view model subscriptions and lifecycle checks when parameters are updated.
    /// </summary>
    protected override void OnParametersSet()
    {
        var vm = (TViewModel?)Context;
        if (ReferenceEquals(vm, _subscribed))
            return;   // same VM: nothing to do

        Unsubscribe();
        _subscribed = vm;
        if (vm is not null)
        {
            if (vm is IMainRouteables routeables)
                routeables.LockChanged += OnLockChanged;

            OnSubscribe();
        }
    }

    /// <summary>
    /// Handles lock change events by triggering a UI state refresh.
    /// </summary>
    private async void OnLockChanged(object? sender, EventArgs e)
    {
        // An event handler (async void): an exception here would reach the circuit. The component may have been removed
        // between the event and the render, which is not an error.
        try { await InvokeAsync(StateHasChanged); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { RouteableErrors.Report(null, ex, typeof(RouteableFor<TView, TViewModel>)); }
    }

    /// <summary>
    /// Unsubscribes from events on the current view model and invokes cleanup hooks.
    /// </summary>
    private void Unsubscribe()
    {
        if (_subscribed is null)
            return;

        if (_subscribed is IMainRouteables routeables)
            routeables.LockChanged -= OnLockChanged;

        OnUnsubscribe();
        _subscribed = null;
    }

    /// <summary>
    /// Extension hook called when a new view model subscription is successfully established.
    /// </summary>
    protected virtual void OnSubscribe()
    {
    }

    /// <summary>
    /// Extension hook called when an existing view model subscription is torn down.
    /// </summary>
    protected virtual void OnUnsubscribe()
    {
    }

    /// <summary>
    /// Disposes the component by tearing down any active event subscriptions.
    /// </summary>
    public void Dispose() => Unsubscribe();

    protected override void OpenComponent(RenderTreeBuilder builder)
    {
        builder.OpenComponent(0, typeof(TView));
    }
}

/// <summary>
/// A specialized variant of <see cref="RouteableFor{TView, TViewModel}"/> that pre-applies a CSS class of <c>"center"</c> 
/// to position the content centrally.
/// </summary>
/// <typeparam name="TView">The Blazor component type representing the view.</typeparam>
/// <typeparam name="TViewModel">The view model type implementing <see cref="INotifyPropertyChanged"/>.</typeparam>
public partial class RouteableCenteredFor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TView, TViewModel> : RouteableFor<TView, TViewModel>
    where TViewModel : class, INotifyPropertyChanged
    where TView : ComponentBase
{
    /// <summary>
    /// Gets or sets the CSS class name(s) applied to the rendered component, defaulting to <c>"center"</c>.
    /// </summary>
    public override string Class { get; set; } = "center";
}