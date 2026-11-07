using Microsoft.Playwright;
using Xunit;

namespace Routeables.Test.Playwright;

/// <summary>
/// Compares a screenshot of each page of the sample with the one kept in <c>Snapshots</c>, in the light and in the dark scheme: a change of
/// colour, spacing or layout that no other test notices fails here. The images are compared by the browser itself (a canvas), so the tests
/// need no image library.
/// <list type="bullet">
/// <item>No image yet: the test writes the screenshot to <c>Snapshots/_actual</c> and is skipped. Look at the images; if they are right, move
/// them to <c>Snapshots</c>. CI uploads <c>_actual</c> as the artifact <c>snapshots-actual</c>.</item>
/// <item>A difference: the test fails and writes the new screenshot to <c>Snapshots/_actual</c>.</item>
/// <item>A change that is wanted: run with <c>ROUTEABLES_UPDATE_SNAPSHOTS=1</c> to replace the images.</item>
/// </list>
/// The images are those of Chromium on Linux (the fonts of another system draw other pixels): make them with CI, not on your own machine.
/// </summary>
[Collection("Blazor")]
public sealed class VisualTests(BlazorApp app) : BlazorTest(app)
{
    /// <summary>The share of pixels allowed to differ (the antialiasing of the text moves a few).</summary>
    private const double AllowedDifference = 0.002;

    // The animations are off and the caret hidden: the screenshot of a page is always the same.
    protected override BrowserNewContextOptions ContextOptions => new()
    {
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        ReducedMotion = ReducedMotion.Reduce,
    };

    private static string SnapshotsFolder
    {
        get
        {
            var root = AppContext.BaseDirectory;
            while (root is not null && !File.Exists(Path.Combine(root, "Routeables.Test.Playwright.csproj")))
                root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
            return Path.Combine(root ?? throw new InvalidOperationException("The folder of the test project was not found."), "Snapshots");
        }
    }

    [Theory]
    [InlineData("Cats", ColorScheme.Light)]
    [InlineData("Cats", ColorScheme.Dark)]
    [InlineData("Dogs", ColorScheme.Light)]
    [InlineData("Dogs", ColorScheme.Dark)]
    [InlineData("About", ColorScheme.Light)]
    [InlineData("About", ColorScheme.Dark)]
    public async Task A_page_looks_as_it_did(string page, ColorScheme scheme)
    {
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = scheme });
        await OpenAsync(page);
        await ExpectCurrentAsync(page);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var actual = await Page.ScreenshotAsync(new PageScreenshotOptions { Animations = ScreenshotAnimations.Disabled, Caret = ScreenshotCaret.Hide });
        var name = $"{page.ToLowerInvariant().Replace(' ', '-')}-{scheme.ToString().ToLowerInvariant()}.png";
        var baseline = Path.Combine(SnapshotsFolder, name);
        var update = Environment.GetEnvironmentVariable("ROUTEABLES_UPDATE_SNAPSHOTS") == "1";

        if (update)
        {
            Directory.CreateDirectory(SnapshotsFolder);
            await File.WriteAllBytesAsync(baseline, actual, TestContext.Current.CancellationToken);
            return;
        }

        if (!File.Exists(baseline))
        {
            Keep(name, actual);
            Assert.Skip($"No image for {name} yet: look at Snapshots/_actual/{name} and move it to Snapshots if it is right.");
        }

        var difference = await DifferenceAsync(await File.ReadAllBytesAsync(baseline, TestContext.Current.CancellationToken), actual);
        if (difference > AllowedDifference)
        {
            Keep(name, actual);
            Assert.Fail($"{name} differs from its image by {difference:P2} of its pixels (allowed {AllowedDifference:P2}). The new screenshot is in Snapshots/_actual.");
        }
    }

    private static void Keep(string name, byte[] image)
    {
        var folder = Path.Combine(SnapshotsFolder, "_actual");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name), image);
    }

    // The share of pixels that differ: both images are drawn on a canvas of the page and read back. A size that differs is a total difference.
    private async Task<double> DifferenceAsync(byte[] expected, byte[] actual)
        => await Page.EvaluateAsync<double>(
            """
            async ([expectedBase64, actualBase64]) => {
                const load = async (base64) => {
                    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
                    const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
                    const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
                    const context = canvas.getContext('2d');
                    context.drawImage(bitmap, 0, 0);
                    return context.getImageData(0, 0, bitmap.width, bitmap.height);
                };
                const a = await load(expectedBase64);
                const b = await load(actualBase64);
                if (a.width !== b.width || a.height !== b.height) return 1;
                let different = 0;
                for (let i = 0; i < a.data.length; i += 4) {
                    const delta = Math.abs(a.data[i] - b.data[i]) + Math.abs(a.data[i + 1] - b.data[i + 1]) + Math.abs(a.data[i + 2] - b.data[i + 2]);
                    if (delta > 48) different++;
                }
                return different / (a.width * a.height);
            }
            """,
            new[] { Convert.ToBase64String(expected), Convert.ToBase64String(actual) });
}
