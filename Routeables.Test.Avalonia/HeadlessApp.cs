using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Routing;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The main view model of the test application: the controls under test need one, not its menu.</summary>
public sealed class TestMain : IMainViewModel
{
    public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
    public IRouteableEntry? Routeable { get; set; }
    public object? CurrentView { get; set; }
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
    public event EventHandler? LockChanged { add { } remove { } }
    public string? CurrentTitle { get; }
}

/// <summary>The application started by the headless session: Fluent for the templates of the controls, then Routeables.</summary>
public sealed class TestApp : Application
{
    /// <summary>The container of the application, set once it is built.</summary>
    public static IServiceProvider Provider { get; private set; } = null!;

    public override void Initialize() => Styles.Add(new FluentTheme());

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
    public const string Side = "side";

    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    public IServiceProvider Provider => TestApp.Provider;

    /// <summary>Runs <paramref name="test"/> on the UI thread of the application.</summary>
    public Task RunAsync(Func<Task> test) => _session.Dispatch(test, CancellationToken.None);

    public void Dispose() => _session.Dispose();
}

[CollectionDefinition("Avalonia headless")]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessApp>;
