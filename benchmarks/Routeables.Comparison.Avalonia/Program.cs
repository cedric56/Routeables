using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Avalonia;
using Routeables.Routing;

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
            .UseRouteables(configure: Configure, afterBuild: provider => Provider = provider);

    /// <summary>
    /// The history of the router has no limit by default, and each entry keeps its view for Back and Forward. To see what that costs,
    /// the variable <c>ROUTEABLES_BENCH_HISTORY_LIMIT</c> (a number) limits the history; the CI runs the memory measure with and without it.
    /// </summary>
    public static int? HistoryLimit { get; } = int.TryParse(Environment.GetEnvironmentVariable("ROUTEABLES_BENCH_HISTORY_LIMIT"), out var n) && n > 0 ? n : null;

    private static void Configure(IServiceCollection services)
    {
        RouteablesAvaloniaPages.Register(services);
        if (HistoryLimit is { } limit)
            services.LimitHistory(limit);
    }
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BenchApp));
        var code = 1;
        // Everything runs on the UI thread of the application, as the code of a real application does.
        await session.Dispatch(
            async () =>
            {
                code = await Runner.RunAsync(args, [() => new RouteablesAvaloniaDriver(), () => new ReactiveUiAvaloniaDriver()], console: false);
                LayoutCount.Report();
            },
            CancellationToken.None);
        return code;
    }
}
