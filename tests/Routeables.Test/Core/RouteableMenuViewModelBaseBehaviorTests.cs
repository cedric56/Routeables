using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Locking;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Core;

public class RouteableMenuViewModelBaseBehaviorTests
{
    private sealed class Entry(string route) : IRouteableMenuEntry
    {
        public bool IsLock { get; set; }
        public object? ViewModel { get; set; }
        public object? View { get; set; }
        public string Route => route;
        public string Template => route;
        public IRouteableArgs Arguments { get; set; } = null!;
        public string DisplayName => route;
        public string? Path => null;
    }

    // A view model that manages routeables asks its router for a lock coordinator, so the container must know how to make one.
    private static Router CreateRouter()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        return new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), services.BuildServiceProvider());
    }

    [Fact]
    public async Task When_the_router_moves_the_entry_of_the_current_view_model_becomes_current_and_its_view_is_announced()
    {
        var router = CreateRouter();
        await GoToAllAsync(router, new CallLog(), "A", "B");

        // The entry is found by the name of the view model's type.
        var entry = new Entry(nameof(RecordingViewModel)) { View = "the view" };
        var sut = TestNavigableViewModel.Create(router, [new Entry("Other"), entry], initialize: false);
        var announced = new List<object?>();
        sut.CurrentViewChanged += (_, view) => announced.Add(view);

        await sut.GoBackAsync();

        Assert.Same(entry, sut.SelectedEntry);
        Assert.Equal([router.CurrentView], announced);
        Assert.Contains(nameof(sut.SelectedEntry), sut.NotifiedProperties);
        Assert.Contains(nameof(sut.CurrentTitle), sut.NotifiedProperties);
    }

    [Fact]
    public async Task When_a_page_pops_the_router_itself_the_menu_follows()
    {
        var router = CreateRouter();
        await GoToAllAsync(router, new CallLog(), "A", "B");
        // The entry shows the view of the page the router goes back to, as an entry of a real menu does.
        var shown = router.History.Entries[0].View;
        var entry = new Entry(nameof(RecordingViewModel)) { View = shown };
        var sut = TestNavigableViewModel.Create(router, [new Entry("Other"), entry], initialize: false);
        var announced = new List<object?>();
        sut.CurrentViewChanged += (_, view) => announced.Add(view);

        // Not through the menu: a button of the page calls the router.
        Assert.True((await router.PopAsync(TestContext.Current.CancellationToken)).Success);

        Assert.Same(entry, sut.SelectedEntry);
        Assert.Same(shown, router.CurrentView);
        Assert.Equal([shown], announced);
    }

    private sealed class FixedRuntime(Runtime current) : IRuntime
    {
        public Runtime Current { get; } = current;
    }

    [Fact]
    public void Only_the_web_and_hybrid_runtimes_are_razor_runtimes()
    {
        Assert.True(new FixedRuntime(Runtime.BlazorServer).IsRazorRuntime());
        Assert.True(new FixedRuntime(Runtime.BlazorWebAssembly).IsRazorRuntime());
        Assert.True(new FixedRuntime(Runtime.MauiHybrid).IsRazorRuntime());
        Assert.False(new FixedRuntime(Runtime.Avalonia).IsRazorRuntime());
        Assert.False(new FixedRuntime(Runtime.Uno).IsRazorRuntime());
        Assert.False(new FixedRuntime(Runtime.Maui).IsRazorRuntime());
        Assert.False(new FixedRuntime(Runtime.Unknown).IsRazorRuntime());
    }

    [Fact]
    public void Uno_is_a_runtime_of_its_own()
    {
        Assert.NotEqual(Runtime.Uno, Runtime.Avalonia);
        Assert.NotEqual(Runtime.Uno, Runtime.Unknown);
        Assert.Equal("Uno", Runtime.Uno.ToString());
    }

    [Fact]
    public void Wpf_is_a_runtime_of_its_own()
    {
        Assert.NotEqual(Runtime.Wpf, Runtime.Uno);
        Assert.NotEqual(Runtime.Wpf, Runtime.Unknown);
        Assert.Equal("Wpf", Runtime.Wpf.ToString());
    }

    private sealed class PageEntry(string route, object page) : IRouteableMenuEntry
    {
        public bool IsLock { get; set; }
        public object? ViewModel { get; set; } = page;
        public object? View { get; set; } = page;
        public string Route => route;
        public string Template => route;
        public IRouteableArgs Arguments { get; set; } = RouteableArgs.Create();
        public string DisplayName => route;
        public string? Path => null;
    }

    [Fact]
    public async Task A_selection_replaced_before_it_started_is_not_the_entry_the_menu_goes_back_to()
    {
        var router = CreateRouter();
        var log = new CallLog();
        RecordingView Page(string name) => new(name, log);
        var a = Page("a");
        var first = new PageEntry("/a", a);
        var second = new PageEntry("/b", Page("b"));
        var third = new PageEntry("/c", Page("c"));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        a.OnEnter = _ => release.Task;
        var sut = TestNavigableViewModel.Create(router, [first, second, third], initialize: false);

        sut.SelectedEntry = first;        // being entered: it holds the gate of the menu
        sut.SelectedEntry = second;       // replaced by the next one before it starts, so its page is never shown
        sut.SelectedEntry = third;
        a.CanLeave = false;               // once shown, the first page refuses to be left: the third selection is refused
        release.SetResult();
        for (var i = 0; i < 200 && router.CurrentView is null; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Same(a, router.CurrentView);
        Assert.Same(first, sut.SelectedEntry);          // the menu shows the page that is displayed, not the one that was skipped
    }
}
