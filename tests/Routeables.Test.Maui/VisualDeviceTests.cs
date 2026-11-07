using Routeables.Abstraction;
using Routeables.Maui;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// A screenshot of a <see cref="RouteableRegion"/> for each page, compared with the image kept in <c>Snapshots</c> (see <c>Shared/Snapshot.cs</c>).
/// The images are those of one platform and one device size: where the application cannot reach the folder of the project (a phone, a
/// simulator) the test is skipped. On the Windows runner of CI the artifact <c>snapshots-maui</c> holds the new ones.
/// </summary>
public class VisualDeviceTests
{
    private static async Task CheckAsync(string route, Type page, string name)
    {
        string folder;
        try { folder = Snapshot.FolderOf("Routeables.Test.Maui.csproj"); }
        catch (InvalidOperationException) { Assert.Skip("The folder of the project is not reachable from this device."); return; }

        await Device.ResetAsync();
        var region = new RouteableRegion { WidthRequest = 480, HeightRequest = 360 };
        using var host = new Hosted<RouteableRegion>(region);
        Assert.True((await Device.Services.GoToAsync(route)).Success);
        await Device.UntilAsync(() => region.Content?.GetType() == page);
        await Task.Delay(300);                                        // one frame: the layout of the page is done

        var png = await VisualDiagnostics.CaptureAsPngAsync(region) ?? throw new InvalidOperationException("Nothing was drawn.");
        var (outcome, message) = Snapshot.Compare(folder, name, png);
        switch (outcome)
        {
            case SnapshotOutcome.NoBaseline: Assert.Skip(message); break;
            case SnapshotOutcome.Different: Assert.Fail(message); break;
        }
    }

    /// <summary>The settings page.</summary>
    [UIFact]
    public Task The_form_page() => CheckAsync("/visual-form", typeof(VisualFormView), "maui-form");

    /// <summary>The about page.</summary>
    [UIFact]
    public Task The_about_page() => CheckAsync("/visual-about", typeof(VisualAboutView), "maui-about");
}
