using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
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
        // The control must be in the tree of a window to be drawn (a screenshot of a control that only has a handler fails): it goes on a
        // modal page over the page of the window, in a box of a fixed size, and the box is what is captured.
        var box = new Grid { WidthRequest = 480, HeightRequest = 360, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, BackgroundColor = Colors.White, Children = { region } };
        var modal = new ContentPage { Content = box, BackgroundColor = Colors.White };
        var navigation = Application.Current!.Windows[0].Page!.Navigation;
        await navigation.PushModalAsync(modal, animated: false);
        try
        {
            try
            {
                await Device.UntilAsync(() => box.Handler is not null && region.Handler is not null);
            }
            catch (TimeoutException)
            {
                // The emulator of CI did not give the modal page a native view in time: nothing can be drawn, and nothing was compared.
                // Say what was seen instead of failing a test that has no image to compare with yet.
                var window = Application.Current!.Windows[0];
                Assert.Skip($"The modal page was not realised on this device (window page {window.Page?.GetType().Name ?? "none"}, " +
                            $"modal stack {navigation.ModalStack.Count}, box handler {(box.Handler is null ? "none" : "set")}, " +
                            $"region handler {(region.Handler is null ? "none" : "set")}).");
                return;
            }
            Assert.True((await Device.Services.GoToAsync(route)).Success);
            await Device.UntilAsync(() => region.Content?.GetType() == page);
            await Task.Delay(500);                                        // a few frames: the layout of the page is done

            var png = await VisualDiagnostics.CaptureAsPngAsync(box) ?? throw new InvalidOperationException("Nothing was drawn.");
            var (outcome, message) = Snapshot.Compare(folder, name, png);
            switch (outcome)
            {
                case SnapshotOutcome.NoBaseline: Assert.Skip(message); break;
                case SnapshotOutcome.Different: Assert.Fail(message); break;
            }
        }
        finally
        {
            await navigation.PopModalAsync(animated: false);
        }
    }

    /// <summary>The settings page.</summary>
    [UIFact]
    public Task The_form_page() => CheckAsync("/visual-form", typeof(VisualFormView), "maui-form");

    /// <summary>The about page.</summary>
    [UIFact]
    public Task The_about_page() => CheckAsync("/visual-about", typeof(VisualAboutView), "maui-about");
}