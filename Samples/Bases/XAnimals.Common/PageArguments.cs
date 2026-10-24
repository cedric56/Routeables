namespace XAnimals.Common;

/// <summary>
/// The keys of the arguments of a page sent to a region (see <c>RegionEntries</c>). They are here so that the list every kind of
/// animal derives from, which lives in this project, can tell it is shown by a region.
/// </summary>
public static class PageArguments
{
    /// <summary>The key of the name of a page in the arguments of its entry.</summary>
    public const string NameKey = "page";

    /// <summary>The key of the icon of a page in the arguments of its entry.</summary>
    public const string IconKey = "icon";

    /// <summary>The key of the region a page was sent to, in the arguments of its entry: a page that finds it is shown by a region.</summary>
    public const string RegionKey = "region";

    /// <summary>The key of the description of a page in the arguments of its entry.</summary>
    public const string DescriptionKey = "description";
}
