using Routeables.Abstraction;

namespace XAnimals.ViewModels;

/// <summary>
/// The icon of each page of the menu. A page names its icon by a key (the second argument of <c>RouteableEntry</c>),
/// and <see cref="IconResolver"/> turns the key into what the platform displays: a glyph (text) for Razor and Avalonia,
/// an <c>ImageSource</c> for Maui, built from the same glyph by the function given to <see cref="Register"/>.
/// </summary>
public static class AnimalIcons
{
    public const string Cat = "cat";
    public const string Dog = "dog";
    public const string Monkey = "monkey";
    public const string Elephant = "elephant";
    public const string Bear = "bear";
    public const string Panda = "panda";
    public const string Lock = "lock";
    public const string History = "history";
    public const string Regions = "regions";
    public const string About = "about";

    private static readonly Dictionary<string, string> glyphs = new()
    {
        [Cat] = "🐱",
        [Dog] = "🐶",
        [Monkey] = "🐵",
        [Elephant] = "🐘",
        [Bear] = "🐻",
        [Panda] = "🐼",
        [Lock] = "🔒",
        [History] = "🧭",
        [Regions] = "🧩",
        [About] = "ℹ️",
    };

    /// <summary>The glyph of an icon key, for a view that draws it itself (a card of the History page).</summary>
    public static string GlyphOf(string key) => glyphs.GetValueOrDefault(key) ?? string.Empty;

    private static bool registered;

    /// <summary>
    /// Registers the resolver once (it is process-wide and cannot be unregistered).
    /// </summary>
    /// <param name="toSource">Builds the platform image from a glyph; leave null where the glyph itself is displayed.</param>
    public static void Register(Func<string, object?>? toSource = null)
    {
        if (registered) return;
        registered = true;
        IconResolver.Register(new GlyphResolver(toSource));
    }

    private sealed class GlyphResolver(Func<string, object?>? toSource) : IIconResolver
    {
        public object? GetIcon(string key) => glyphs.GetValueOrDefault(key);

        public object? GetSource(string key)
            => toSource is not null && glyphs.TryGetValue(key, out var glyph) ? toSource(glyph) : null;
    }
}
