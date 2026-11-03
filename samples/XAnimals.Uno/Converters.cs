using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using XAnimals.ViewModels;

namespace XAnimals.Uno;

/// <summary>true => Visible (false => Visible with <see cref="Invert"/>).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is true) != Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

/// <summary>Not null => Visible (null => Visible with <see cref="Invert"/>).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is not null) != Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language) => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

/// <summary>The background of a card of the History page: the current entry, the ones ahead of it (forward), the past ones. Gets the HistoryItem.</summary>
public sealed class HistoryCardBrushConverter : IValueConverter
{
    public Brush? Current { get; set; }
    public Brush? Ahead { get; set; }
    public Brush? Past { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language)
        => value is HistoryItem { IsCurrent: true } ? Current : value is HistoryItem { IsAhead: true } ? Ahead : Past;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

/// <summary>
/// An address becomes an image. The bytes are downloaded here, with a User-Agent: Wikimedia refuses the requests of a BitmapImage
/// built on an address, which send none.
/// </summary>
public sealed class AnimalImageConverter : IValueConverter
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        try { client.DefaultRequestHeaders.UserAgent.ParseAdd("RouteablesSamples/1.0 (https://github.com/cedric56/Routeables)"); }
        catch { /* A browser does not let the page set it: the request goes without. */ }
        return client;
    }

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string { Length: > 0 } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var image = new BitmapImage();
        _ = LoadAsync(image, uri);
        return image;
    }

    private static async Task LoadAsync(BitmapImage image, Uri uri)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(uri);
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            // Not disposed, and no DetachStream: Uno does not implement it, and disposing the writer would close the stream.
            var writer = new Windows.Storage.Streams.DataWriter(stream);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            stream.Seek(0);
            await image.SetSourceAsync(stream);
        }
        catch
        {
            // No picture: the card keeps its plain background.
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
