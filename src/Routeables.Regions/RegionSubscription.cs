namespace Routeables.Regions;

/// <summary>Runs an action once, when disposed: what <c>AttachRouteables</c> returns to stop following a control.</summary>
public sealed class RegionSubscription(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}
