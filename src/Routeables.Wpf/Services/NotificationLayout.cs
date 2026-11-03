namespace Routeables.Wpf.Services;

/// <summary>
/// Where the open notifications go, in device pixels. A notification is a window of its own; it may be created on one monitor and
/// shown on another, so the placement is done on the pixels of the screen (what <c>Window.Left</c> and <c>Window.Top</c> cannot do
/// when the monitors have different scales: they are units of the monitor the window is on, which is not the one it is moved to).
/// The scale is that of the window the notifications belong to: the margin and the gap are the same size to the eye on any monitor.
/// </summary>
internal static class NotificationLayout
{
    /// <summary>The distance from the top of the window, in device-independent units.</summary>
    public const double Margin = 16;

    /// <summary>The distance between two notifications, in device-independent units.</summary>
    public const double Gap = 8;

    /// <summary>
    /// The top left corner of each notification, in pixels: centred on the window, the first one under its top edge, the others
    /// under the one before.
    /// </summary>
    /// <param name="ownerLeft">The left edge of the area of the owner window, in pixels of the screen.</param>
    /// <param name="ownerTop">The top edge of that area, in pixels of the screen.</param>
    /// <param name="ownerWidth">The width of that area, in pixels.</param>
    /// <param name="scale">The scale of the owner (1.5 for 150 %): pixels per device-independent unit.</param>
    /// <param name="sizes">The size of each notification in pixels, oldest first.</param>
    /// <returns>The position of each notification, in the order of <paramref name="sizes"/>.</returns>
    public static IReadOnlyList<(int X, int Y)> Stack(int ownerLeft, int ownerTop, int ownerWidth, double scale, IReadOnlyList<(int Width, int Height)> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "The scale is a number of pixels per unit: greater than zero.");

        var gap = (int)Math.Round(Gap * scale);
        var top = ownerTop + (int)Math.Round(Margin * scale);
        var places = new List<(int X, int Y)>(sizes.Count);

        foreach (var (width, height) in sizes)
        {
            places.Add((ownerLeft + (ownerWidth - width) / 2, top));
            top += height + gap;
        }

        return places;
    }
}
