using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Avalonia;
using System.Collections.ObjectModel;
using Xunit;

[assembly: AssemblyFixture(typeof(Routeables.Test.Avalonia.Visual.VisualApp))]

namespace Routeables.Test.Avalonia.Visual;

/// <summary>The main view model of the test application: the controls under test need one, not its menu.</summary>
public sealed class TestMain : IMainViewModel
{
    /// <inheritdoc />
    public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];

    /// <inheritdoc />
    public IRouteableMenuEntry? SelectedEntry { get; set; }

    /// <inheritdoc />
    public object? CurrentView { get; set; }

    /// <inheritdoc />
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

    /// <inheritdoc />
    public event EventHandler? LockChanged { add { } remove { } }

    /// <inheritdoc />
    public string? CurrentTitle { get; }
}

/// <summary>A page with the controls an application puts on one: a title, a text, a form and buttons.</summary>
public sealed class FormView : RouteableFor<FormViewModel>
{
    /// <summary>Builds the page.</summary>
    public FormView() => Content = new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 12,
        Children =
        {
            new TextBlock { Text = "Settings", FontSize = 24, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "Choose how the application behaves.", TextWrapping = TextWrapping.Wrap },
            new TextBox { Text = "Routeables", PlaceholderText = "Name" },
            new CheckBox { Content = "Keep me signed in", IsChecked = true },
            new ComboBox { ItemsSource = new[] { "Light", "Dark", "System" }, SelectedIndex = 1 },
            new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                Children = { new Button { Content = "Save" }, new Button { Content = "Cancel" } },
            },
        },
    };
}

/// <summary>View model of <see cref="FormView"/>.</summary>
public sealed class FormViewModel;

/// <summary>A second page, so the region shows another view after a route change.</summary>
public sealed class AboutView : RouteableFor<AboutViewModel>
{
    /// <summary>Builds the page.</summary>
    public AboutView() => Content = new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 12,
        Children =
        {
            new TextBlock { Text = "About", FontSize = 24, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "Routeables routes between pages on every platform.", TextWrapping = TextWrapping.Wrap },
            new ProgressBar { Value = 60, Minimum = 0, Maximum = 100 },
        },
    };
}

/// <summary>View model of <see cref="AboutView"/>.</summary>
public sealed class AboutViewModel;

/// <summary>The application the headless session starts: Fluent with the Inter font (the same letters on every machine), then Routeables.</summary>
public sealed class VisualApplication : Application
{
    /// <summary>The container of the application, set once it is built.</summary>
    public static IServiceProvider Provider { get; private set; } = null!;

    /// <summary>Adds the Fluent theme required by the control templates.</summary>
    public override void Initialize() => Styles.Add(new FluentTheme());

    /// <summary>Configures the headless platform with real drawing, the font and the routes.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<VisualApplication>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseRouteables<TestMain>(
                configure: services =>
                {
                    services.RegisterRoute<FormView, FormViewModel>(route: "/form");
                    services.RegisterRoute<AboutView, AboutViewModel>(route: "/about");
                },
                afterBuild: provider => Provider = provider);
}

/// <summary>One headless application with drawing for all the tests (a process has one <see cref="Application"/>).</summary>
public sealed class VisualApp : IDisposable
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(VisualApplication));

    /// <summary>The container of the running application.</summary>
    public IServiceProvider Provider => VisualApplication.Provider;

    /// <summary>Runs <paramref name="test"/> on the UI thread of the application.</summary>
    public Task RunAsync(Func<Task> test) => _session.Dispatch(test, CancellationToken.None);

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();
}
