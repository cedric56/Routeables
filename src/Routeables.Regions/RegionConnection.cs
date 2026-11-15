using Routeables.Abstraction.Routing;

namespace Routeables.Regions;

/// <summary>
/// The life of a region host (<c>RouteableRegion</c>, <c>RouteableRegionItems</c>) without any UI: connects to the router of its region when the
/// control appears, lets go of it when the control leaves, and says whether a late post may still show something.
/// </summary>
public sealed class RegionConnection
{
    private IDisposable? _binding;

    /// <summary>
    /// Whether the control is connected. Set <b>before</b> the binding exists, since a binding shows the view of now as soon as it is built:
    /// a post that arrives after <see cref="Disconnect"/> must show nothing.
    /// </summary>
    public bool Active { get; private set; }

    /// <summary>Whether a binding exists: the control is connected to a router.</summary>
    public bool IsConnected => _binding is not null;

    /// <summary>
    /// Connects unless already connected or without a container.
    /// </summary>
    /// <param name="provider">The container to read the region from, the explicit one or the one of the application; <see langword="null"/> connects nothing.</param>
    /// <param name="region">The name of the region; empty for the default one.</param>
    /// <param name="bind">Builds the binding from the router; it may show the first view at once.</param>
    /// <returns>Whether the control is connected now by this call.</returns>
    public bool Connect(IServiceProvider? provider, string? region, Func<IRouter, IDisposable> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        if (_binding is not null || provider is null)
            return false;

        Active = true;
        try
        {
            _binding = bind(provider.GetRouter(string.IsNullOrEmpty(region) ? null : region));
        }
        catch
        {
            Active = false;     // no binding: nothing is connected, and a late show must not be applied
            throw;
        }
        return true;
    }

    /// <summary>
    /// What <c>RouteableRegion</c> does when it appears: shows the current view of the region, then starts the routing of an application
    /// that has no menu (<see cref="RegionStart.Begin"/>).
    /// </summary>
    /// <param name="provider">The container to read the region from; <see langword="null"/> connects nothing.</param>
    /// <param name="region">The name of the region; empty for the default one.</param>
    /// <param name="route">The page the region opens when its router shows nothing yet, or <see langword="null"/>.</param>
    /// <param name="show">Shows a view; called on any thread, the host puts it on its own.</param>
    /// <returns>Whether the control is connected now by this call.</returns>
    public bool ConnectCurrent(IServiceProvider? provider, string? region, string? route, Action<object?, RegionChange> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        // The binding shows the current view as soon as it is built.
        if (!Connect(provider, region, router => new RegionBinding(router, show)))
            return false;

        // The default region starts the routing of an application that has no menu (the binding is there to show the first page).
        RegionStart.Begin(provider!, region, route);
        return true;
    }

    /// <summary>What <c>RouteableRegionItems</c> does when it appears: shows every view of the region, now and when it changes.</summary>
    /// <param name="provider">The container to read the region from; <see langword="null"/> connects nothing.</param>
    /// <param name="region">The name of the region; empty for the default one.</param>
    /// <param name="show">Shows the views; called on any thread, the host puts it on its own.</param>
    /// <returns>Whether the control is connected now by this call.</returns>
    public bool ConnectItems(IServiceProvider? provider, string? region, Action<IReadOnlyList<object>> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        // The binding gives the views of now as soon as it is built.
        return Connect(provider, region, router => new RegionItemsBinding(router, show));
    }

    /// <summary>Lets go of the router. Clear the content afterwards: a view has one parent and must be free for another control.</summary>
    public void Disconnect()
    {
        Active = false;
        var binding = _binding;
        _binding = null;
        binding?.Dispose();
    }
}
