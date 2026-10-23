using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Routeables.Locking;

namespace Routeables.Tests.Core;

public class RouteableViewModelBaseTests
{
    private sealed class Entry(string route) : IRouteableEntry
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

        Assert.Same(entry, sut.Routeable);
        Assert.Equal("the view", sut.CurrentView);
        Assert.Equal(["the view"], announced);
        Assert.Contains(nameof(sut.Routeable), sut.NotifiedProperties);
        Assert.Contains(nameof(sut.CurrentView), sut.NotifiedProperties);
    }

    [Fact]
    public void Setting_the_current_view_announces_it()
    {
        var sut = TestNavigableViewModel.Create(CreateRouter(), [], initialize: false);
        var announced = new List<object?>();
        sut.CurrentViewChanged += (_, view) => announced.Add(view);

        sut.CurrentView = "v";

        Assert.Equal(["v"], announced);
        Assert.Contains(nameof(sut.CurrentView), sut.NotifiedProperties);
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
}
