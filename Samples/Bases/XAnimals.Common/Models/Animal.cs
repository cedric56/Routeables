namespace XAminals.Models;
public class Animal
{
    public required string Name { get; set; }
    public required string Location { get; set; }
    public required string Details { get; set; }
    public required string ImageUrl { get; set; }

    /// <summary>The image for a list: a small copy of <see cref="ImageUrl"/> (see <see cref="ImageSizes"/>).</summary>
    public string ThumbnailUrl => ImageSizes.Resize(ImageUrl, ImageSizes.List);

    /// <summary>The image for a detail view: a copy of <see cref="ImageUrl"/> sized for the page.</summary>
    public string PhotoUrl => ImageSizes.Resize(ImageUrl, ImageSizes.Detail);
}
