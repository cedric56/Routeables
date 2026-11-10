using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Avalonia;
using Routeables.Routing;
using System.Collections.ObjectModel;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The menu view model of the test application: the controls under test need one, not its menu.</summary>
public sealed class TestMain : IRouteableMenuViewModel
{
    /// <summary>The collection of available routeable entries.</summary>
    public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];

    /// <summary>The currently selected routeable entry, if any.</summary>
    public IRouteableMenuEntry? SelectedEntry { get; set; }

    /// <summary>The view currently displayed by the main content area.</summary>
    public object? CurrentView { get; set; }

    /// <summary>Raised when <see cref="CurrentView"/> changes. Empty implementation for headless tests.</summary>
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

    /// <summary>Raised when the lock state changes. Empty implementation for headless tests.</summary>
    public event EventHandler? LockChanged { add { } remove { } }

    /// <summary>The title of the current view. Always null in this test fixture.</summary>
    public string? CurrentTitle { get; }
}

/// <summary>The application started by the headless session: Fluent for the templates of the controls, then Routeables.</summary>
public sealed class TestApp : Application
{
    /// <summary>The container of the application, set once it is built.</summary>
    public static IServiceProvider Provider { get; private set; } = null!;

    /// <summary>Adds the Fluent theme required by the control templates.</summary>
    public override void Initialize() => Styles.Add(new FluentTheme());

    /// <summary>Configures and builds the headless Avalonia application with the test routes and side region.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseRouteables<TestMain>(
                configure: services =>
                {
                    services.RegisterRoute<PageAView, PageAViewModel>(route: "/a");
                    services.RegisterRoute<PageBView, PageBViewModel>(route: "/b");
                    services.AddRegion(HeadlessApp.Side);
                },
                afterBuild: provider => Provider = provider);
}

/// <summary>One headless Avalonia application for all the tests (a process has one <see cref="Application"/>).</summary>
public sealed class HeadlessApp : IDisposable
{
    /// <summary>The name of the side region registered for tests.</summary>
    public const string Side = "side";

    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    /// <summary>The service provider of the running test application.</summary>
    public IServiceProvider Provider => TestApp.Provider;

    /// <summary>Runs <paramref name="test"/> on the UI thread of the application.</summary>
    public Task RunAsync(Func<Task> test) => _session.Dispatch(test, CancellationToken.None);

    /// <summary>Disposes the headless session.</summary>
    public void Dispose() => _session.Dispose();
}

/// <summary>xUnit collection that shares a single <see cref="HeadlessApp"/> instance across tests.</summary>
[CollectionDefinition("Avalonia headless")]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessApp>;