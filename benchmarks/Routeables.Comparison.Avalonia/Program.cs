using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Routeables.Avalonia;

namespace Routeables.Comparison.Avalonia;

/// <summary>The application the benchmark runs in: Fluent for the templates of the controls, then Routeables with its pages.</summary>
public sealed class BenchApp : Application
{
    /// <summary>The container of Routeables, set once it is built.</summary>
    public static IServiceProvider Provider { get; private set; } = null!;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<BenchApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseRouteables(configure: RouteablesAvaloniaPages.Register, afterBuild: provider => Provider = provider);
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BenchApp));
        var code = 1;
        // Everything runs on the UI thread of the application, as the code of a real application does.
        await session.Dispatch(
            async () => code = await Runner.RunAsync(args, [() => new RouteablesAvaloniaDriver(), () => new ReactiveUiAvaloniaDriver()], console: false),
            CancellationToken.None);
        return code;
    }
}
