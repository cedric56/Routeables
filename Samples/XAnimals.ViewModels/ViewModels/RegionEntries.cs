using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace XAnimals.ViewModels;

/// <summary>
/// Builds the entries the sample pages send to their regions. An entry is a menu entry with a <see cref="IRouteableEntry.Region"/>:
/// <c>provider.GoToAsync(entry)</c> sends it to the router of that region, and the <c>RouteableRegion</c> of that name shows it.
/// </summary>
public static class RegionEntries
{
    /// <summary>The key of the name of a page in the arguments of its entry.</summary>
    public const string NameKey = "page";

    /// <summary>The key of the icon of a page in the arguments of its entry.</summary>
    public const string IconKey = "icon";

    /// <summary>The key of the region a page was sent to, in the arguments of its entry: a page that finds it is shown by a region.</summary>
    public const string RegionKey = "region";

    /// <summary>The key of the description of a page in the arguments of its entry.</summary>
    public const string DescriptionKey = "description";

    // The pages a region shows: the animal lists of the menu. Not the History or Regions pages (they would show themselves),
    // nor the pages that act on the application (Lock) or open dialogs (About).
    private static readonly string[] PageRoutes = ["/cats", "/dogs", "/monkeys", "/elephants", "/bears", "/pandas"];

    /// <summary>The animal pages of the main menu, in the order of the menu.</summary>
    public static IReadOnlyList<IRouteableEntry> Pages(IServiceProvider provider)
        => provider.GetRequiredService<IMainRouteables>().Routeables
            .Where(entry => PageRoutes.Contains(entry.Route))
            .ToList();

    /// <summary>
    /// The same page as the menu entry, but an entry of its own: the router of the region builds its own view and view model,
    /// and keeps them apart from the ones the main menu remembers. The name and the icon travel with the entry.
    /// </summary>
    public static RouteableEntry Page(IRouteableEntry source, string region)
    {
        // A copy of the entry, of the same type: the router of the region builds its own view and view model for it.
        if (source is not RouteableEntry entry)
            throw new InvalidOperationException($"The entry {source.Route} is not a RouteableEntry.");

        var page = entry.Copy();
        page.Region = region;
        page.Arguments = RouteableArgs.Create()
            .WithValue(NameKey, source.DisplayName)
            .WithValue(IconKey, source.Path)
            .WithValue(RegionKey, region)
            .WithValue(DescriptionKey, source.Description?.Split('\n')[0].Trim() ?? string.Empty);
        return page;
    }

    /// <summary>A bar (header or footer) for <paramref name="region"/>; the router builds a new one each time.</summary>
    public static RouteableEntry Bar(string region, ShellBarInfo info)
        => new RouteableEntry<ShellBarViewModel>("Bar", null, route: "shell-bar", template: "shell-bar")
        {
            Region = region,
            Arguments = RouteableArgs.Create().ForViewModel(info)
        };

    /// <summary>The menu of the left panel.</summary>
    public static RouteableEntry Menu(string region)
        => new RouteableEntry<ShellMenuViewModel>("Menu", null, route: "shell-menu", template: "shell-menu") { Region = region };

    /// <summary>Whether the entry was sent as an animal page (it carries its name), not opened from inside one (a detail).</summary>
    public static bool HasName(IRouterEntry? entry) => ValueOf(entry, NameKey) is not null;

    /// <summary>The name of the page an entry of a region holds.</summary>
    public static string? NameOf(IRouterEntry? entry)
        => ValueOf(entry, NameKey) ?? entry?.ViewModel.GetType().Name.Replace("ViewModel", string.Empty);

    /// <summary>The glyph of the page an entry of a region holds.</summary>
    public static string GlyphOf(IRouterEntry? entry)
        => AnimalIcons.GlyphOf(ValueOf(entry, IconKey) ?? string.Empty) is { Length: > 0 } glyph ? glyph : "📄";

    /// <summary>The first line of the description of the page an entry of a region holds.</summary>
    public static string DescriptionOf(IRouterEntry? entry) => ValueOf(entry, DescriptionKey) ?? string.Empty;

    private static string? ValueOf(IRouterEntry? entry, string key)
        => entry?.Args.Values.FirstOrDefault(v => v.name == key)?.value as string;
}
