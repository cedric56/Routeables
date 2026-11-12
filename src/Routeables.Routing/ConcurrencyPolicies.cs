namespace Routeables.Routing;

internal sealed class ConcurrencyPolicies
{
    private readonly Dictionary<string, RouterConcurrency> _byRegion = [];
    private RouterConcurrency? _default;

    public void Set(string? region, RouterConcurrency policy)
    {
        if (region is null) _default = policy;
        else _byRegion[region] = policy;
    }

    public RouterConcurrency For(string? region)
        => (region is null ? _default : _byRegion.TryGetValue(region, out var p) ? p : null) ?? RouterConcurrency.Queue;
}
