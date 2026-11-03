using Routeables.Abstraction;

namespace Routeables.Regions;

/// <summary>
/// An application made of several regions around one that leads: a header, a footer, a menu on the side and a content, for
/// instance. This is a common layout, and this class is everything it needs besides what it shows.
/// <para>
/// The <em>primary</em> region is the one the user routes (a list opens its detail in it, Back and Forward move it). The other
/// regions follow it: <see cref="OnPrimaryChangedAsync"/> runs after each move, never while the primary router holds its gate,
/// on the thread that started the layout, and one run after the other. Starting, clearing, disposing and the thread are done here.
/// </para>
/// <code>
/// public sealed class Shell(IServiceProvider provider) : RegionLayout(provider, "content", "header", "menu")
/// {
///     protected override async Task OnStartAsync(CancellationToken ct)
///     {
///         await GoToAsync("menu", menuEntry, ct);
///         await ShowAsync(firstPage, ct);
///     }
///
///     protected override Task OnPrimaryChangedAsync()
///         => FollowAsync("header", PrimaryRouter.History.Current, () => HeaderFor(PrimaryRouter.History.Current));
/// }
/// // services.AddRegionLayout&lt;Shell&gt;("content", "header", "menu");   a RouteableRegion of each name in the view
/// </code>
/// </summary>
public abstract class RegionLayout : IDisposable
{
    private readonly Dictionary<string, object?> _shown = [];
    private RegionFollower? _follower;

    /// <param name="provider">The provider (scope) the regions are registered in.</param>
    /// <param name="primary">The region the user routes; the others follow it.</param>
    /// <param name="followers">The other regions.</param>
    protected RegionLayout(IServiceProvider provider, string primary, params string[] followers)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(primary);
        Provider = provider;
        Primary = primary;
        Regions = [primary, .. followers];
    }

    /// <summary>The container the routers of the regions come from.</summary>
    protected IServiceProvider Provider { get; }

    /// <summary>The name of the region the user routes.</summary>
    public string Primary { get; }

    /// <summary>The names of all the regions, the primary one first.</summary>
    public IReadOnlyList<string> Regions { get; }

    /// <summary>The router of the primary region.</summary>
    protected IRouter PrimaryRouter => Provider.GetRouter(Primary);

    /// <summary>
    /// Whether the other regions follow the primary one. Set it to <see langword="false"/> while they show something else
    /// (a credits page, a modal flow), and back to <see langword="true"/> to follow again at the next move.
    /// </summary>
    protected bool Following { get; set; }

    /// <summary>Raised when the primary region moved (on the thread that started the layout): a menu updates its selection.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when <see cref="OnPrimaryChangedAsync"/> failed: a region that cannot follow does not break the primary one.</summary>
    public event EventHandler<Exception>? Failed;

    /// <summary>Whether the primary region has an entry to go back to.</summary>
    public bool CanGoBack => PrimaryRouter.CanGoBack;

    /// <summary>Whether the primary region has an entry to go forward to.</summary>
    public bool CanGoForward => PrimaryRouter.CanGoForward;

    /// <summary>Starts over: every region is emptied, the others follow the primary one, then <see cref="OnStartAsync"/> fills them.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await ClearAsync(cancellationToken);
        Following = true;

        // Created here, where the layout is started (the UI thread): that is where the regions follow.
        _follower = new RegionFollower(PrimaryRouter, OnMovedAsync);
        _follower.Failed += (_, ex) => Failed?.Invoke(this, ex);
        await OnStartAsync(cancellationToken);
    }

    /// <summary>Empties every region: their pages are disposed and the layout stops following.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _follower?.Dispose();
        _follower = null;
        _shown.Clear();
        await Provider.EraseRegionsAsync(Regions, cancellationToken);
    }

    /// <summary>Shows <paramref name="view"/> in the primary region; the others follow.</summary>
    public async Task<RouteableResult> ShowAsync(IRouteableEntry view, CancellationToken cancellationToken = default)
    {
        var result = await PrimaryRouter.GoToRouteAsync(view, cancellationToken);
        await SettledAsync();
        return result;
    }

    /// <summary>Goes back in the primary region; the others follow it.</summary>
    public async Task<RouteableResult> BackAsync(CancellationToken cancellationToken = default)
    {
        var result = await PrimaryRouter.GoBackAsync(cancellationToken);
        await SettledAsync();
        return result;
    }

    /// <summary>Goes forward in the primary region; the others follow it.</summary>
    public async Task<RouteableResult> ForwardAsync(CancellationToken cancellationToken = default)
    {
        var result = await PrimaryRouter.ForwardAsync(cancellationToken);
        await SettledAsync();
        return result;
    }

    /// <summary>Completes when the other regions say what the primary one shows now.</summary>
    public Task SettledAsync() => _follower?.SettledAsync() ?? Task.CompletedTask;

    /// <summary>What the layout shows when it starts: the pages of the regions. The others are already following.</summary>
    protected abstract Task OnStartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The primary region moved: update the others. Use <see cref="FollowAsync"/> for each; it does nothing when what a region
    /// shows would not change. Not called while <see cref="Following"/> is <see langword="false"/> or the primary is empty.
    /// </summary>
    protected virtual Task OnPrimaryChangedAsync() => Task.CompletedTask;

    /// <summary>Shows <paramref name="view"/> in the region it names, whichever it is.</summary>
    protected Task<RouteableResult> GoToAsync(string region, IRouteableEntry view, CancellationToken cancellationToken = default)
        => Provider.GetRouter(region).GoToRouteAsync(view, cancellationToken);

    /// <summary>
    /// Updates <paramref name="region"/> to <paramref name="view"/>, unless <paramref name="key"/> (a record or a value that
    /// compares by content) is what that region shows already. The view is only built when it is needed.
    /// </summary>
    protected async Task FollowAsync(string region, object? key, Func<IRouteableEntry> view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (_shown.TryGetValue(region, out var shown) && Equals(shown, key)) return;
        _shown[region] = key;
        await GoToAsync(region, view());
    }

    /// <summary>Tells <see cref="Changed"/> listeners that the layout moved without the primary region moving.</summary>
    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private Task OnMovedAsync()
    {
        RaiseChanged();
        return Following && PrimaryRouter.History.Count > 0 ? OnPrimaryChangedAsync() : Task.CompletedTask;
    }

    /// <summary>Stops following the primary region.</summary>
    public void Dispose()
    {
        _follower?.Dispose();
        _follower = null;
    }
}
