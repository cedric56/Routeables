using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test;
using Routeables.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using FontWeights = System.Windows.FontWeights;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using TextBox = System.Windows.Controls.TextBox;
using Thickness = System.Windows.Thickness;
using Window = System.Windows.Window;
using Routeables.Abstraction.Registrations;
namespace Routeables.Tests.Wpf;

/// <summary>
/// A screenshot of a <see cref="RouteableRegion"/> for each page, compared with the image kept in <c>Snapshots</c> (<see cref="Snapshot"/>): a
/// change of colour, spacing or layout of the controls that no other test notices fails here. The images are those of WPF on the Windows
/// runner of CI (its fonts and its rendering): make them there, with the artifact <c>snapshots-wpf</c>.
/// </summary>
[Collection(WpfCollection.Name)]
public class WpfVisualTests
{
    private sealed class FormView : RouteableFor
    {
        public FormView() => Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock { Text = "Settings", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) },
                new TextBlock { Text = "Choose how the application behaves.", Margin = new Thickness(0, 0, 0, 12) },
                new TextBox { Text = "Routeables", Margin = new Thickness(0, 0, 0, 12) },
                new CheckBox { Content = "Keep me signed in", IsChecked = true, Margin = new Thickness(0, 0, 0, 12) },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { new Button { Content = "Save", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) }, new Button { Content = "Cancel", Padding = new Thickness(12, 4, 12, 4) } },
                },
            },
        };
    }

    private sealed class AboutView : RouteableFor
    {
        public AboutView() => Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock { Text = "About", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) },
                new TextBlock { Text = "Routeables routes between pages on every platform.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) },
                new ProgressBar { Value = 60, Minimum = 0, Maximum = 100, Height = 16 },
            },
        };
    }

    private sealed class FormViewModel;
    private sealed class AboutViewModel;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddRegion("visual");
        services.AddSingleton<IRouter>(sp => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(sp), sp));
        services.RegisterRoute<FormView, FormViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/form");
        services.RegisterRoute<AboutView, AboutViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/about");
        services.AddTransient<FormViewModel>();
        services.AddTransient<AboutViewModel>();
        return services.BuildServiceProvider();
    }

    private static async Task CheckAsync(string route, string template, string name)
    {
        using var provider = Build();
        var region = await WpfHarness.Run(() => new RouteableRegion { Region = "visual", Services = provider });
        var window = await WpfHarness.Run(() =>
        {
            var w = new Window { Content = region, Width = 480, Height = 360, ShowActivated = false, ShowInTaskbar = false };
            w.Show();
            return w;
        });

        try
        {
            var router = provider.GetRouter("visual");
            Assert.True((await WpfHarness.Run(() => router.GoToAsync(route, template: template, cancellationToken: TestContext.Current.CancellationToken))).Success);
            await WpfHarness.Idle();

            var png = await WpfHarness.Run(() =>
            {
                region.Measure(new Size(480, 360));
                region.Arrange(new Rect(0, 0, 480, 360));
                region.UpdateLayout();
                var bitmap = new RenderTargetBitmap(480, 360, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(region);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                return stream.ToArray();
            });

            var (outcome, message) = Snapshot.Compare(Snapshot.FolderOf("Routeables.Test.csproj"), name, png);
            switch (outcome)
            {
                case SnapshotOutcome.NoBaseline: Assert.Skip(message); break;
                case SnapshotOutcome.Different: Assert.Fail(message); break;
            }
        }
        finally
        {
            await WpfHarness.Run(() => window.Close());
            await WpfHarness.Idle();
        }
    }

    [Fact]
    public Task The_form_page() => CheckAsync("/form", "/form", "wpf-form");

    [Fact]
    public Task The_about_page() => CheckAsync("/about", "/about", "wpf-about");
}
