using System.Windows.Automation.Peers;

namespace Routeables.Wpf.Automation;

// What a screen reader and a UI test see of the controls of Routeables: a group per region and per view, named after what they show, with a stable automation id
// (the one the author gave wins). The content of each control is still its own: only the role, the name and the id are added.
internal static class PeerNames
{
    /// <summary>The name of the view model a <c>RouteableFor&lt;TViewModel&gt;</c> is for, without the suffix: <c>HelloViewModel</c> gives <c>Hello</c>.</summary>
    public static string ForName(object control)
    {
        var type = control.GetType();
        while (type is not null && !type.IsGenericType)
            type = type.BaseType;

        var name = type?.GetGenericArguments().FirstOrDefault()?.Name ?? "View";
        return name.EndsWith("ViewModel", StringComparison.Ordinal) && name.Length > "ViewModel".Length ? name[..^"ViewModel".Length] : name;
    }

    public static string Region(string? region) => string.IsNullOrEmpty(region) ? "main" : region;

    public static string Named(string? given, string fallback) => string.IsNullOrEmpty(given) ? fallback : given;
}

internal sealed class RouteableRegionAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly RouteableRegion _owner;

    public RouteableRegionAutomationPeer(RouteableRegion owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(RouteableRegion);

    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), PeerNames.Region(_owner.Region));

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), "Region." + PeerNames.Region(_owner.Region));
}

internal sealed class RouteableRegionItemsAutomationPeer : ItemsControlAutomationPeer
{
    private readonly RouteableRegionItems _owner;

    public RouteableRegionItemsAutomationPeer(RouteableRegionItems owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(RouteableRegionItems);

    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), PeerNames.Region(_owner.Region));

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), "RegionItems." + PeerNames.Region(_owner.Region));
}

internal sealed class RouteableForAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly RouteableFor _owner;

    public RouteableForAutomationPeer(RouteableFor owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(RouteableFor);

    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), PeerNames.ForName(_owner));

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), "View." + PeerNames.ForName(_owner));
}
