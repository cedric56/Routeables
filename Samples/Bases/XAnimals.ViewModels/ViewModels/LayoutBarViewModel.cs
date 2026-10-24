namespace XAnimals.ViewModels;

/// <summary>What a bar of the Regions page says.</summary>
/// <param name="Glyph">The icon at the start of the bar.</param>
/// <param name="Title">The main text.</param>
/// <param name="Detail">The line under it.</param>
/// <param name="Strong">Drawn in the strong color (the header), or in the quiet one (the footer).</param>
public sealed record LayoutBarInfo(string Glyph, string Title, string Detail, bool Strong = false)
{
    /// <summary>A bar that says nothing: every text is <see cref="string.Empty"/>.</summary>
    public static readonly LayoutBarInfo Empty = new(string.Empty, string.Empty, string.Empty);
}

/// <summary>
/// The view model of a bar of the Regions page: the header, the footer, and the line of the left panel. The same view and view
/// model serve all of them, each in a region of its own; it does not know where it is shown. The router builds one for each
/// entry or segment, from the <see cref="LayoutBarInfo"/> it carries (matched to the constructor by type,
/// <c>ForViewModel(info)</c>).
/// </summary>
public class LayoutBarViewModel(LayoutBarInfo info)
{
    public string Glyph { get; } = info.Glyph;

    public string Title { get; } = info.Title;

    public string Detail { get; } = info.Detail;

    public bool IsStrong { get; } = info.Strong;
}
