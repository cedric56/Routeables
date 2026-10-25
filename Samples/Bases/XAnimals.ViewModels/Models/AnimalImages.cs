using Xaminals.Data;

namespace XAminals.Models;

/// <summary>
/// The images every host loads while the application starts, so that a page shows them at once instead of fetching them when the
/// user opens it. The lists come in the order of the menu: the first pages are ready first.
/// </summary>
public static class AnimalImages
{
    /// <summary>The list images (see <see cref="ImageSizes.List"/>) of every animal page, once each.</summary>
    public static IReadOnlyList<string> Thumbnails { get; } = new[]
        {
            CatData.Cats, DogData.Dogs, MonkeyData.Monkeys, ElephantData.Elephants, BearData.Bears, PandaData.Pandas,
        }
        .SelectMany(animals => animals)
        .Select(animal => animal.ThumbnailUrl)
        .Distinct()
        .ToArray();
}
