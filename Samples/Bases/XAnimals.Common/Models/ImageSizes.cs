namespace XAminals.Models;

/// <summary>
/// The pictures of the animals are the originals of Wikimedia Commons, several megabytes each, shown in a thumbnail of 56 by 68.
/// Wikimedia makes a smaller copy on request: <c>…/commons/8/8d/X.jpg</c> becomes <c>…/commons/thumb/8/8d/X.jpg/250px-X.jpg</c>.
/// Only the widths Wikimedia keeps in its cache are used (…, 250, 330, 500, 960, …): another width is made on request, which is slow.
/// </summary>
public static class ImageSizes
{
    /// <summary>The width of a list image (56 points, with room for a dense screen).</summary>
    public const int List = 250;

    /// <summary>The width of a detail image (260 points, with room for a dense screen).</summary>
    public const int Detail = 500;

    private const string Commons = "https://upload.wikimedia.org/wikipedia/commons/";

    /// <summary>
    /// The address of the copy of <paramref name="url"/> that is <paramref name="width"/> wide. An address that is not an original
    /// of Commons is returned as it is.
    /// </summary>
    public static string Resize(string url, int width)
    {
        if (url is null || !url.StartsWith(Commons, StringComparison.Ordinal))
            return url!;

        var path = url[Commons.Length..];
        var parts = path.Split('/');

        // Original: a/ab/Name.jpg. A thumb already, or any other shape, stays as it is.
        if (parts.Length != 3 || parts[0] == "thumb")
            return url;

        return $"{Commons}thumb/{path}/{width}px-{parts[2]}";
    }

    /// <summary>
    /// The original of a copy made by <see cref="Resize"/>, for the case where Wikimedia refuses the copy (a width above that of the
    /// original). An address that is not such a copy is returned as it is.
    /// </summary>
    public static string Original(string url)
    {
        const string Thumb = Commons + "thumb/";
        if (url is null || !url.StartsWith(Thumb, StringComparison.Ordinal))
            return url!;

        // thumb/a/ab/Name.jpg/250px-Name.jpg -> a/ab/Name.jpg
        var parts = url[Thumb.Length..].Split('/');
        return parts.Length == 4 ? $"{Commons}{parts[0]}/{parts[1]}/{parts[2]}" : url;
    }
}
