using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using XAnimals.ViewModels;

namespace XAnimals.Wpf;

/// <summary>
/// An address becomes an image. The bytes are downloaded here, with a User-Agent: Wikimedia refuses the requests of a
/// BitmapImage built on an address, which send none. Use it with <c>IsAsync=True</c>: it runs on a thread of the pool.
/// </summary>
public sealed class AnimalImageConverter : IValueConverter
{
    /// <summary>
    /// The client of the images, shared with the MAUI and Avalonia samples: <see cref="CachedImageHandler"/> keeps the answers, runs two
    /// downloads at a time and waits when Wikimedia answers 429 (too many requests), which a plain client does as soon as a page
    /// asks for a dozen images together.
    /// </summary>
    public static HttpClient Http { get; } = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new CachedImageHandler(new HttpClientHandler()));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RouteablesSamples/1.0 (https://github.com/cedric56/Routeables)");
        return client;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        try
        {
            // This runs on a thread of the pool (IsAsync), where nothing is waiting for it. The handler answers through its async
            // path only, so the call is the async one: waiting for it here blocks no UI thread.
            var bytes = Http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;   // decoded now: the stream is not kept
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();                                  // usable on the UI thread, which did not create it
            return image;
        }
        catch
        {
            // No picture: the card keeps its plain background.
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
