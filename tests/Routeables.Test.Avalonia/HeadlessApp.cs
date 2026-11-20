using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Avalonia;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
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

    // The first window the application shows is its main window: closing it ends the application, and Routeables then disposes the container.
    // A test closes the windows it opens, so this one is opened first and stays open until the end.
    private Window? _main;
    private ITopLevelStateManager? _activatedOn;

    /// <summary>Starts the session and opens the main window of the application.</summary>
    public HeadlessApp() => _session.Dispatch(() =>
    {
        _main = new Window { Width = 800, Height = 600 };
        _main.Show();
        Dispatcher.UIThread.RunJobs();
    }, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>The service provider of the running test application.</summary>
    public IServiceProvider Provider => TestApp.Provider;

    /// <summary>Runs <paramref name="test"/> on the UI thread of the application.</summary>
    public async Task RunAsync(Func<Task> test)
    {
        // Dispatch returns before the body of an async job ends (the dialog tests then passed with their assertions never run), and nothing pumps
        // the UI thread once it has returned: a test that really waits (Task.Delay) never resumed. So the job is not async: it starts the test,
        // then runs the jobs of the UI thread itself until the test is over.
        Exception? failure = null;
        await _session.Dispatch(() =>
        {
            try
            {
                // Routeables takes the first window that is active after its setup for the main one, and its setup replaces the state manager
                // after the session started (more than once, as far as the tests can tell). So before each test, the main window is made the active
                // one on the manager there is, unless it already was: no window of a test is the first one.
                var manager = TopLevelStateManager.Default;
                if (!ReferenceEquals(manager, _activatedOn))
                {
                    _activatedOn = manager;
                    manager.OnActivated(_main!);
                }

                var running = test();
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (!running.IsCompleted)
                {
                    if (DateTime.UtcNow > deadline)
                        throw new TimeoutException("The body of the test did not end within 60 seconds.");
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(1);
                }

                running.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }, CancellationToken.None);

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>Disposes the headless session.</summary>
    public void Dispose()
    {
        _session.Dispatch(() => _main?.Close(), CancellationToken.None).GetAwaiter().GetResult();
        _session.Dispose();
    }
}

/// <summary>xUnit collection that shares a single <see cref="HeadlessApp"/> instance across tests.</summary>
[CollectionDefinition("Avalonia headless")]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessApp>;