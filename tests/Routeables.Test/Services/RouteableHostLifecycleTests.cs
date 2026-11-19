using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Core;

namespace Routeables.Tests.Services;

/// <summary>What every host does when its first window is activated and when its last one closes.</summary>
public class RouteableHostLifecycleTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Error)> Reports { get; } = [];
        public void Report(string source, Exception exception) => Reports.Add((source, exception));
    }

    private sealed class FailingMenu : IRouteableMenuViewModel
    {
        public System.Collections.ObjectModel.ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];
        public IRouteableMenuEntry? SelectedEntry { get; set; }
        public object? CurrentView { get; set; }
        public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
        public event EventHandler? LockChanged { add { } remove { } }
        public string? CurrentTitle { get; }
        bool IRouteableMenuViewModel.SelectFirst() => throw new InvalidOperationException("no first entry");
    }

    private static ServiceProvider Provider(IRouteableMenuViewModel? menu, Sink sink)
    {
        var services = new ServiceCollection().AddSingleton<IRouteableErrorSink>(sink);
        if (menu is not null)
            services.AddSingleton(menu);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void SelectFirst_selects_the_first_entry_of_the_menu()
    {
        var menu = new FakeMenuViewModel();
        var first = new RouteableMenuEntry("/a", "/a", "A");
        menu.Entries.Add(first);
        menu.Entries.Add(new RouteableMenuEntry("/b", "/b", "B"));
        var sink = new Sink();

        RouteableHostLifecycle.SelectFirst(Provider(menu, sink), typeof(RouteableHostLifecycleTests));

        Assert.Same(first, menu.SelectedEntry);
        Assert.Empty(sink.Reports);
    }

    [Fact]
    public void SelectFirst_does_nothing_without_a_menu()
    {
        var sink = new Sink();

        RouteableHostLifecycle.SelectFirst(Provider(null, sink), typeof(RouteableHostLifecycleTests));

        Assert.Empty(sink.Reports);
    }

    [Fact]
    public void SelectFirst_reports_a_failure_and_does_not_throw()
    {
        var sink = new Sink();

        RouteableHostLifecycle.SelectFirst(Provider(new FailingMenu(), sink), typeof(RouteableHostLifecycleTests));

        var report = Assert.Single(sink.Reports);
        Assert.Equal("no first entry", report.Error.Message);
        Assert.Contains(nameof(RouteableHostLifecycleTests), report.Source);
    }

    [Fact]
    public void RunOnDispose_runs_the_callback_of_the_application()
    {
        var ran = 0;

        RouteableHostLifecycle.RunOnDispose(Provider(null, new Sink()), () => ran++, typeof(RouteableHostLifecycleTests));

        Assert.Equal(1, ran);
    }

    [Fact]
    public void RunOnDispose_accepts_no_callback()
    {
        var sink = new Sink();

        RouteableHostLifecycle.RunOnDispose(Provider(null, sink), null, typeof(RouteableHostLifecycleTests));

        Assert.Empty(sink.Reports);
    }

    [Fact]
    public void RunOnDispose_reports_a_failing_callback_and_does_not_throw()
    {
        var sink = new Sink();

        RouteableHostLifecycle.RunOnDispose(Provider(null, sink), () => throw new InvalidOperationException("boom"), typeof(RouteableHostLifecycleTests));

        Assert.Equal("boom", Assert.Single(sink.Reports).Error.Message);
    }
}
