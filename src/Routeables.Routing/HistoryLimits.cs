namespace Routeables.Routing;

/// <summary>The history limits set with <see cref="Extensions.LimitHistory"/>, by region (<see langword="null"/>: the default router).</summary>
internal sealed class HistoryLimits
{
    private readonly Dictionary<string, int> _byRegion = [];
    private int? _default;

    public void Set(string? region, int maxSize)
    {
        if (region is null) _default = maxSize;
        else _byRegion[region] = maxSize;
    }

    public int? For(string? region)
        => region is null ? _default : _byRegion.TryGetValue(region, out var size) ? size : null;
}
