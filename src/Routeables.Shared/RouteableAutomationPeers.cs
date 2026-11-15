#if AVALONIA
using Avalonia.Automation.Peers;
using RegionPeerBase = Avalonia.Automation.Peers.ContentControlAutomationPeer;
using RegionItemsPeerBase = Avalonia.Automation.Peers.ItemsControlAutomationPeer;
using ForPeerBase = Avalonia.Automation.Peers.ContentControlAutomationPeer;
#elif WPF
using System.Windows.Automation.Peers;
using RegionPeerBase = System.Windows.Automation.Peers.FrameworkElementAutomationPeer;
using RegionItemsPeerBase = System.Windows.Automation.Peers.FrameworkElementAutomationPeer;
using ForPeerBase = System.Windows.Automation.Peers.FrameworkElementAutomationPeer;
#else
using Microsoft.UI.Xaml.Automation.Peers;
using RegionPeerBase = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer;
using RegionItemsPeerBase = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer;
using ForPeerBase = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer;
#endif

// One file for every host with automation peers: Routeables.Wpf and Routeables.Avalonia link it (and define WPF or AVALONIA), Routeables.Uno and
// Routeables.WinUI3 link it too: the peers say the same, only their base class differs. The item of the menu is for the hosts that have one (Uno, WinUI 3).
#if AVALONIA
namespace Routeables.Avalonia.Automation;
#elif WPF
namespace Routeables.Wpf.Automation;
#elif WINUI3
namespace Routeables.WinUI3.Automation;
#else
namespace Routeables.Uno.Automation;
#endif

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

internal sealed partial class RouteableRegionAutomationPeer : RegionPeerBase
{
    private readonly RouteableRegion _owner;

    public RouteableRegionAutomationPeer(RouteableRegion owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(RouteableRegion);

    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), PeerNames.Region(_owner.Region));

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), "Region." + PeerNames.Region(_owner.Region));
}

internal sealed partial class RouteableRegionItemsAutomationPeer : RegionItemsPeerBase
{
    private readonly RouteableRegionItems _owner;

    public RouteableRegionItemsAutomationPeer(RouteableRegionItems owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(RouteableRegionItems);

    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), PeerNames.Region(_owner.Region));

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), "RegionItems." + PeerNames.Region(_owner.Region));
}

internal sealed partial class RouteableForAutomationPeer : ForPeerBase
{
    private readonly RouteableFor _owner;

    public RouteableForAutomationPeer(RouteableFor owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(RouteableFor);

    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), PeerNames.ForName(_owner));

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), "View." + PeerNames.ForName(_owner));
}

#if !WPF && !AVALONIA
internal sealed partial class RouteableMenuItemAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly RouteableMenuItem _owner;

    public RouteableMenuItemAutomationPeer(RouteableMenuItem owner) : base(owner) => _owner = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => nameof(RouteableMenuItem);

    // The entry names the item, and its route identifies it.
    protected override string GetNameCore() => PeerNames.Named(base.GetNameCore(), _owner.Item?.DisplayName ?? string.Empty);

    protected override string GetAutomationIdCore() => PeerNames.Named(base.GetAutomationIdCore(), _owner.Item is { } item ? "MenuItem." + item.Route : string.Empty);
}
#endif
