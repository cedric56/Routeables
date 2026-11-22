namespace Routeables.Routing;

/// <summary>The history limits set with <see cref="Extensions.LimitHistory"/> and <see cref="Extensions.KeepWholeHistory"/>, by region (<see langword="null"/>: the default router).</summary>
internal sealed class HistoryLimits
{
    private readonly Dictionary<string, int?> _byRegion = [];
    private bool _defaultSet;
    private int? _default;

    /// <summary>Sets the limit of a region; <see langword="null"/> keeps every entry.</summary>
    public void Set(string? region, int? maxSize)
    {
        if (region is null) { _defaultSet = true; _default = maxSize; }
        else _byRegion[region] = maxSize;
    }

    /// <summary>The limit asked for a region, when one was (a <see langword="null"/> size is "no limit").</summary>
    public bool TryGet(string? region, out int? size)
    {
        if (region is null) { size = _default; return _defaultSet; }
        return _byRegion.TryGetValue(region, out size);
    }
}
