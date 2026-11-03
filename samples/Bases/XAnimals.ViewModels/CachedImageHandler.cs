using System.Collections.Concurrent;
using System.Net;
using XAminals.Models;

namespace XAnimals.ViewModels;

/// <summary>
/// The handler of the HttpClient that loads the images, shared by the MAUI and Avalonia samples. It keeps the answers in memory
/// (at most the images of the animals: two per animal), so that images loaded ahead (see <see cref="PreloadAsync"/>) are not
/// downloaded again when a page asks for them, and it limits how many downloads run together.
/// Wikimedia answers 429 when a client asks for too many images at once: a refused download waits for the delay the server gives.
/// A thumbnail that Wikimedia refuses (wider than the original) is replaced by the original, and an image that cannot be loaded at
/// all by <see cref="ImagePlaceholder"/>, which is not kept: the next request tries again.
/// </summary>
public sealed class CachedImageHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Uri, Lazy<Task<Answer>>> cache = new();
    private readonly SemaphoreSlim gate = new(2);

    private sealed record Answer(HttpStatusCode Status, byte[] Bytes, string? ContentType);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri is not { } uri)
            return await base.SendAsync(request, cancellationToken);

        var entry = cache.GetOrAdd(uri, u => new Lazy<Task<Answer>>(() => LoadAsync(u, request)));
        Answer answer;
        try
        {
            answer = await entry.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The caller gave up; the download goes on for the others.
            throw;
        }
        catch
        {
            // The network failed: the placeholder, and a new try at the next request.
            cache.TryRemove(uri, out _);
            answer = new Answer(HttpStatusCode.OK, ImagePlaceholder.Png, "image/png");
        }

        // A failure is not kept: the next request tries again. The page shows the placeholder meanwhile.
        if ((int)answer.Status >= 400)
        {
            cache.TryRemove(uri, out _);
            answer = new Answer(HttpStatusCode.OK, ImagePlaceholder.Png, "image/png");
        }

        var response = new HttpResponseMessage(answer.Status) { Content = new ByteArrayContent(answer.Bytes), RequestMessage = request };
        if (answer.ContentType is not null)
            response.Content.Headers.TryAddWithoutValidation("Content-Type", answer.ContentType);
        return response;
    }

    private async Task<Answer> LoadAsync(Uri uri, HttpRequestMessage template)
    {
        var answer = await DownloadAsync(uri, template);
        if ((int)answer.Status >= 400 && ImageSizes.Original(uri.AbsoluteUri) is var original && original != uri.AbsoluteUri)
            answer = await DownloadAsync(new Uri(original), template);
        return answer;
    }

    private async Task<Answer> DownloadAsync(Uri uri, HttpRequestMessage template)
    {
        await gate.WaitAsync();
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                // A request is sent once: every attempt gets a copy of the headers the client set (the User-Agent).
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                foreach (var header in template.Headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

                using var response = await base.SendAsync(request, CancellationToken.None);
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRetries)
                {
                    var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 << attempt);
                    await Task.Delay(wait > MaxWait ? MaxWait : wait);
                    continue;
                }

                var bytes = response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : [];
                return new Answer(response.StatusCode, bytes, response.Content.Headers.ContentType?.ToString());
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Loads the images one after the other, in the background; a page that needs one of them meanwhile is not kept waiting.</summary>
    public static async Task PreloadAsync(HttpClient client, IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            try
            {
                using var response = await client.GetAsync(url);
            }
            catch
            {
                // Preloading is a convenience: the page loads the image itself when it needs it.
            }
        }
    }
}
