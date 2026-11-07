using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia.Visual;

/// <summary>
/// A screenshot of a <see cref="RouteableRegion"/> in a window for each page, in the light and the dark theme, compared with the image kept
/// in <c>Snapshots</c> (<see cref="Snapshot"/>): a change of colour, spacing or layout of the controls that no other test notices fails here.
/// </summary>
public sealed class VisualTests(VisualApp app)
{
    /// <summary>Waits on the UI thread until <paramref name="condition"/> holds.</summary>
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    private Task CheckAsync(string route, string page, ThemeVariant theme, string name) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        await provider.GetRouter(null).EraseHistoryAsync();

        var region = new RouteableRegion();
        var window = new Window { Width = 480, Height = 360, RequestedThemeVariant = theme, Content = region };
        window.Show();
        Assert.True((await provider.GoToAsync(route)).Success);
        await UntilAsync(() => region.Content?.GetType().Name == page);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window drew nothing: is Skia on?");
        using var stream = new MemoryStream();
        frame.Save(stream);
        window.Close();

        var (outcome, message) = Snapshot.Compare(Snapshot.FolderOf("Routeables.Test.Avalonia.Visual.csproj"), name, stream.ToArray());
        switch (outcome)
        {
            case SnapshotOutcome.NoBaseline: Assert.Skip(message); break;
            case SnapshotOutcome.Different: Assert.Fail(message); break;
        }
    });

    /// <summary>The settings page in the light theme.</summary>
    [Fact]
    public Task The_form_page_in_the_light_theme() => CheckAsync("/form", nameof(FormView), ThemeVariant.Light, "form-light");

    /// <summary>The settings page in the dark theme.</summary>
    [Fact]
    public Task The_form_page_in_the_dark_theme() => CheckAsync("/form", nameof(FormView), ThemeVariant.Dark, "form-dark");

    /// <summary>The about page in the light theme.</summary>
    [Fact]
    public Task The_about_page_in_the_light_theme() => CheckAsync("/about", nameof(AboutView), ThemeVariant.Light, "about-light");

    /// <summary>The about page in the dark theme.</summary>
    [Fact]
    public Task The_about_page_in_the_dark_theme() => CheckAsync("/about", nameof(AboutView), ThemeVariant.Dark, "about-dark");
}
