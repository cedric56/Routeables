using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XAnimals.ViewModels;

namespace XAnimals.Wpf.Views;

/// <summary>
/// Gives a <see cref="Border"/> the picture of an address as its background: <c>views:AnimalPicture.Url="{Binding PhotoUrl}"</c>. The
/// picture is downloaded without waiting: no thread is held while Wikimedia answers (a binding with IsAsync waits on a thread of the
/// pool, and a page of a dozen pictures starves the pool, which freezes the pages that are being changed). The border stays empty until
/// it comes. It is a property of the border, not of an ImageBrush: a brush has no element to find the data context from.
/// </summary>
public static class AnimalPicture
{
    /// <summary>
    /// The client of the images, shared with the MAUI and Avalonia samples: <see cref="CachedImageHandler"/> keeps the answers, runs two
    /// downloads at a time and waits when Wikimedia answers 429 (too many requests).
    /// </summary>
    public static HttpClient Http { get; } = CreateClient();

    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.RegisterAttached("Url", typeof(string), typeof(AnimalPicture), new PropertyMetadata(null, OnUrlChanged));

    public static string? GetUrl(DependencyObject element) => (string?)element.GetValue(UrlProperty);

    public static void SetUrl(DependencyObject element, string? value) => element.SetValue(UrlProperty, value);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new CachedImageHandler(new HttpClientHandler()));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RouteablesSamples/1.0 (https://github.com/cedric56/Routeables)");
        return client;
    }

    private static async void OnUrlChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Border border)
            return;

        border.Background = null;
        if (e.NewValue is not string { Length: > 0 } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        try
        {
            // Both awaits resume on the UI thread; the decoding runs on the pool and the image is frozen, so that the UI thread can use it.
            var bytes = await Http.GetByteArrayAsync(uri);
            var image = await Task.Run(() => Decode(bytes));

            // The border was given another address while this one came (a card that is reused): the last address wins.
            if (GetUrl(border) == url)
                border.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        }
        catch
        {
            // No picture: the card keeps its plain background.
        }
    }

    private static BitmapImage Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;   // decoded now: the stream is not kept
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
