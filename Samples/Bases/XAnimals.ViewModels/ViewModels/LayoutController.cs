using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Segments;

namespace XAnimals.ViewModels;

/// <summary>
/// What the Regions page shows in its four regions, on top of <see cref="RegionLayout"/> which does the rest (starting and
/// clearing the regions, Back and Forward, following the content on the UI thread). Each region is a router with its own history,
/// and a <c>RouteableRegion</c> of the same name in the view shows it.
/// <para>
/// The content navigates by itself (a list opens its detail in the same region, and Back returns to it): the header and the footer
/// follow whatever moved it.
/// </para>
/// </summary>
public sealed class LayoutController(IServiceProvider provider) : RegionLayout(provider, Content, Header, Footer, LeftPanel)
{
    public const string Header = "shell-header";
    public const string Footer = "shell-footer";
    public const string LeftPanel = "shell-left";
    public const string Content = "shell-content";

    /// <summary>The names of the segments the credits navigation uses.</summary>
    public const string BarSegment = "Bar";
    public const string LogoSegment = "Logo";

    /// <summary>Whether the page shows the credits: the bars then say what the credits say, not what the content shows.</summary>
    public bool IsCredits => !Following;

    /// <summary>The name of the animal page the content is in (its list, also while one of its details is open).</summary>
    public string? Current => Section(PrimaryRouter.History) is { } section ? RegionEntries.NameOf(section) : null;

    /// <summary>The animal pages the left panel offers.</summary>
    public IReadOnlyList<IRouteableEntry> Pages => RegionEntries.Pages(Provider);

    /// <summary>The menu in the left panel, the first page in the content.</summary>
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        await GoToAsync(LeftPanel, RegionEntries.Menu(LeftPanel), ct);
        await ShowAsync(RegionEntries.Page(Pages[0], Content), ct);
    }

    /// <summary>Shows a page in the content (the header and the footer follow).</summary>
    public Task<RouteableResult> ShowPageAsync(IRouteableEntry source, CancellationToken ct = default)
        => ShowAsync(RegionEntries.Page(source, Content), ct);

    /// <summary>
    /// Shows the credits in the four regions with one navigation made of segments, one per region: the logo in the content, the
    /// name of the author in the footer, what the application is in the header, the copyright in the left panel. The navigation is
    /// absolute (the histories of the four regions are cleared first). <see cref="StartAsync"/> brings the application back.
    /// </summary>
    public async Task<RouteableResult> ShowCreditsAsync(IRouteCoordinator coordinator, CancellationToken ct = default)
    {
        // The bars follow the content only while the application is shown.
        Following = false;
        await SettledAsync();

        // A segment is a name and the region that handles it; what the view model needs is given to it by its arguments.
        static Action<IRouteableArgs> Bar(LayoutBarInfo info) => args => args.ForViewModel(info);

        var credits = RouteCoordination.Create()
            .Absolute()
            .Segment(BarSegment, Bar(LayoutBarInfo.Empty), LeftPanel)
            .Segment(LogoSegment, Content)
            .Segment(BarSegment, Bar(new LayoutBarInfo("©", "© 2026 Cedric", "Released under the MIT License", Strong: true)), Header)
            .Segment(BarSegment, Bar(new LayoutBarInfo("✍️", "Cedric", "Author of Routeables")), Footer)
            .Build();

        var result = await coordinator.CoordinateAsync(credits, ct);
        RaiseChanged();
        return result;
    }

    // The entry of the content that holds an animal page: the list. Its details have no name of their own.
    private static IHistoryEntry? Section(IRouterHistory history)
    {
        for (var i = history.CurrentIndex; i >= 0; i--)
            if (RegionEntries.HasName(history.Entries[i]))
                return history.Entries[i];

        return null;
    }

    // The header and the footer say what the content shows now.
    protected override async Task OnPrimaryChangedAsync()
    {
        var history = PrimaryRouter.History;
        var section = Section(history);
        var current = history.Current;
        var detail = ReferenceEquals(section, current)
            ? RegionEntries.DescriptionOf(section)
            : $"Opened: {RegionEntries.NameOf(current)}";

        var header = new LayoutBarInfo(RegionEntries.GlyphOf(section), RegionEntries.NameOf(section) ?? string.Empty, detail, Strong: true);
        await FollowAsync(Header, header, () => RegionEntries.Bar(Header, header));

        var footer = new LayoutBarInfo("🧭", $"Content: entry {history.CurrentIndex + 1} of {history.Count}",
            $"Back: {(history.CanGoBack ? "yes" : "no")} · Forward: {(history.CanGoForward ? "yes" : "no")}");
        await FollowAsync(Footer, footer, () => RegionEntries.Bar(Footer, footer));
    }
}
