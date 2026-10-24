using System.Globalization;
using XAnimals.ViewModels;

namespace XAnimals.Maui;

/// <summary>
/// Loads an image URL with a User-Agent: Wikimedia answers 403 Forbidden to the HttpClient used by
/// <see cref="UriImageSource"/>, which sends none. The downloads, their limit, the retries after a 429, the memory cache and the
/// placeholder of an image that cannot be loaded are those of <see cref="CachedImageHandler"/>, shared with the Avalonia sample.
/// </summary>
public sealed class WebImageConverter : IValueConverter
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new CachedImageHandler(new HttpClientHandler())) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XAnimals/1.0 (https://github.com/cedric56/Routeables)");
        return client;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string url && url.Length > 0
            ? ImageSource.FromStream(ct => Http.GetStreamAsync(url, ct))
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>
    /// Loads the images one after the other, in the background, while the application starts: a page finds them in memory.
    /// One at a time, so that an image a page asks for meanwhile does not wait behind the others.
    /// </summary>
    public static Task PreloadAsync(IEnumerable<string> urls) => CachedImageHandler.PreloadAsync(Http, urls);
}
