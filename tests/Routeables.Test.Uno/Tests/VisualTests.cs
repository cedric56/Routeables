using Microsoft.UI.Xaml.Media.Imaging;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Uno;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary>
/// A screenshot of a <see cref="RouteableRegion"/> for each page, compared with the image kept in <c>Snapshots</c> (see <c>Shared/Snapshot.cs</c>).
/// The images are those of the Skia head on Linux of CI (<c>ui-uno</c>): make them there, with the artifact <c>snapshots-uno</c>. Without an
/// image the test is inconclusive.
/// </summary>
[TestClass]
[RunsOnUIThread]
public class VisualTests
{
    private static async Task CheckAsync(string route, Type page, string name)
    {
        await TestHost.ResetAsync();
        var region = new RouteableRegion { Width = 480, Height = 360 };
        await TestHost.LoadAsync(region);
        try
        {
            Assert.IsTrue((await TestHost.Services.GoToAsync(route)).Success);
            await TestHost.UntilAsync(() => region.Content?.GetType() == page);
            await Task.Delay(100);                                    // one frame: the layout of the page is done

            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(region);
            var buffer = await bitmap.GetPixelsAsync();
            var pixels = new byte[buffer.Length];                      // BGRA, row after row
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                reader.ReadBytes(pixels);
            var png = Routeables.Test.Snapshot.Encode(bitmap.PixelWidth, bitmap.PixelHeight, Routeables.Test.Snapshot.BgraToRgba(pixels));

            var (outcome, message) = Routeables.Test.Snapshot.Compare(Routeables.Test.Snapshot.FolderOf("Routeables.Test.Uno.csproj"), name, png);
            switch (outcome)
            {
                case Routeables.Test.SnapshotOutcome.NoBaseline: Assert.Inconclusive(message); break;
                case Routeables.Test.SnapshotOutcome.Different: Assert.Fail(message); break;
            }
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    /// <summary>The settings page.</summary>
    [TestMethod]
    public Task The_form_page() => CheckAsync("/visual-form", typeof(VisualFormView), "uno-form");

    /// <summary>The about page.</summary>
    [TestMethod]
    public Task The_about_page() => CheckAsync("/visual-about", typeof(VisualAboutView), "uno-about");
}
