using Routeables.Abstraction;
using Routeables.Services;
using Routeables.Tests.Core;

namespace Routeables.Tests.Services;

public class LockWatcherTests
{
    private sealed class Entry(bool locked) : IRouteableEntry
    {
        public bool IsLock { get; set; } = locked;
        public object? ViewModel { get; set; }
        public object? View { get; set; }
        public string Route => "/entry";
        public string Template => "/entry";
        public IRouteableArgs Arguments { get; set; } = null!;
        public string DisplayName => "entry";
        public string? Path => null;
    }

    private static FakeMainRouteables MainWith(params bool[] locked)
    {
        var main = new FakeMainRouteables();
        foreach (var isLocked in locked)
        {
            main.Routeables.Add(new Entry(isLocked));
        }

        return main;
    }

    private static void Lock(FakeMainRouteables main, bool locked)
    {
        foreach (var entry in main.Routeables)
            entry.IsLock = locked;

        main.LockChanged?.Invoke(main, EventArgs.Empty);
    }

    [Fact]
    public void Locked_means_at_least_one_entry_and_all_of_them_locked()
    {
        Assert.False(RouteableLockWatcher.AllLocked(null));
        Assert.False(RouteableLockWatcher.AllLocked(MainWith()));
        Assert.False(RouteableLockWatcher.AllLocked(MainWith(true, false)));
        Assert.True(RouteableLockWatcher.AllLocked(MainWith(true, true)));
    }

    [Fact]
    public void Follow_returns_the_current_state_and_reports_every_change()
    {
        var main = MainWith(false, false);
        var seen = new List<bool>();
        using var watcher = new RouteableLockWatcher(seen.Add);

        Assert.False(watcher.Follow(main));

        Lock(main, true);
        Lock(main, false);

        Assert.Equal([true, false], seen);
    }

    [Fact]
    public void Following_again_keeps_a_single_subscription()
    {
        var main = MainWith(false);
        var count = 0;
        using var watcher = new RouteableLockWatcher(_ => count++);

        watcher.Follow(main);
        watcher.Follow(main);
        watcher.Follow(main);
        Lock(main, true);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Something_that_is_not_a_main_view_model_is_not_followed_and_dispose_stops_the_report()
    {
        var main = MainWith(false);
        var count = 0;
        var watcher = new RouteableLockWatcher(_ => count++);

        Assert.False(watcher.Follow("not a main view model"));

        watcher.Follow(main);
        watcher.Dispose();
        Lock(main, true);

        Assert.Equal(0, count);
        Assert.Null(main.LockChanged);
    }
}