using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Core;

/// <summary>RouteableAppLifecycle tells the shown pages (main router and regions) that the application sleeps or resumes.</summary>
public class AppLifecycleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Aware : RecordingViewModel, IRouteableAppLifecycle
    {
        private readonly CallLog _calls;

        public Aware(string name, CallLog log) : base(name, log) => _calls = log;

        public Exception? SleepFails { get; set; }
        public Task OnSleepAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add($"{Name}:Sleep");
            return SleepFails is null ? Task.CompletedTask : Task.FromException(SleepFails);
        }
        public Task OnResumeAsync(CancellationToken cancellationToken = default) { _calls.Add($"{Name}:Resume"); return Task.CompletedTask; }
    }

    private static async Task<(RouteableAppLifecycle app, CallLog log, Aware main, Aware side)> SetupAsync()
    {
        var log = new CallLog();
        var mainRouter = CreateRouter();
        var sideRouter = CreateRouter();
        var services = new ServiceCollection();
        services.AddSingleton<IRouter>(mainRouter);
        services.AddKeyedSingleton<IRouter>("side", sideRouter);
        RouteableRegionNames.Record(services, "side");
        var provider = services.BuildServiceProvider();

        var old = new Aware("old", log);
        var main = new Aware("main", log);
        var side = new Aware("side", log);
        Assert.True((await mainRouter.GoAsync(new RecordingView("v1", log), old, null, Ct)).Success);
        Assert.True((await mainRouter.GoAsync(new RecordingView("v2", log), main, null, Ct)).Success);
        Assert.True((await sideRouter.GoAsync(new RecordingView("v3", log), side, null, Ct)).Success);
        log.Clear();
        return (new RouteableAppLifecycle(provider), log, main, side);
    }

    [Fact]
    public async Task Sleep_AndResume_TellOnlyThePagesThatAreShown_InTheMainRouterAndTheRegions()
    {
        var (app, log, _, _) = await SetupAsync();

        await app.SleepAsync(Ct);
        Assert.Equal(["main:Sleep", "side:Sleep"], log.Entries.Where(e => e.EndsWith("Sleep")).ToArray());
        Assert.DoesNotContain("old:Sleep", log.Entries);

        log.Clear();
        await app.ResumeAsync(Ct);
        Assert.Equal(["main:Resume", "side:Resume"], log.Entries.Where(e => e.EndsWith("Resume")).ToArray());
    }

    [Fact]
    public async Task APageThatThrows_DoesNotStopTheOthersBeingTold()
    {
        var (app, log, main, _) = await SetupAsync();
        main.SleepFails = new InvalidOperationException("boom");

        await app.SleepAsync(Ct);

        Assert.Contains("side:Sleep", log.Entries);
    }

    [Fact]
    public async Task SleepNow_ReturnsTrue_WhenThePagesFinish()
    {
        var (app, log, _, _) = await SetupAsync();

        Assert.True(app.SleepNow());
        Assert.Contains("main:Sleep", log.Entries);
    }

    [Fact]
    public void AddRegion_RecordsTheRegionName()
    {
        var services = new ServiceCollection();
        services.AddRegion("side");
        services.AddRegion("side");
        services.AddRegion("other");

        Assert.Equal(["other", "side"], RouteableRegionNames.For(services).All.OrderBy(n => n).ToArray());
    }
}
