// The test renderer is a Renderer: it has to read the render tree frames, which the analyzer discourages outside the framework.
#pragma warning disable BL0006
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Tests.Pages;
using Dispatcher = Microsoft.AspNetCore.Components.Dispatcher;
namespace Routeables.Tests.Razor;

/// <summary>The component that shows every view of a region: one cell per view of the history, oldest first, following the router.</summary>
public class RouteableRegionItemsTests
{
    // The views that are on screen now, in order: a view is added when it is rendered and removed when its cell goes away.
    private static readonly List<string> Alive = [];

    private sealed class Cell : ComponentBase, IDisposable
    {
        [Parameter] public object? Context { get; set; }
        protected override void OnInitialized() { lock (Alive) Alive.Add(Context!.GetType().Name); }
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "cell");
        public void Dispose() { lock (Alive) Alive.Remove(Context!.GetType().Name); }
    }

    private sealed class FirstView : RouteableFor { protected override void OpenComponent(RenderTreeBuilder builder) => builder.OpenComponent<Cell>(0); }
    private sealed class SecondView : RouteableFor { protected override void OpenComponent(RenderTreeBuilder builder) => builder.OpenComponent<Cell>(0); }

    private sealed class TestRenderer(IServiceProvider provider) : Renderer(provider, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<(string Name, string? Value)> Attributes { get; } = [];

        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            for (var i = 0; i < batch.ReferenceFrames.Count; i++)
            {
                var frame = batch.ReferenceFrames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Attribute)
                    Attributes.Add((frame.AttributeName, frame.AttributeValue?.ToString()));
            }

            return Task.CompletedTask;
        }

        protected override void HandleException(Exception exception) => throw exception;

        public async Task<int> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
        {
            var id = AssignRootComponentId(InstantiateComponent(typeof(T)));
            await Dispatcher.InvokeAsync(() => RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters)));
            return id;
        }

        public Task Update(int id, Dictionary<string, object?> parameters)
            => Dispatcher.InvokeAsync(() => RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters)));
    }

    private static ServiceProvider Build()
    {
        lock (Alive) Alive.Clear();
        var services = new ServiceCollection();
        services.AddRegion("a");
        services.AddRegion("b");
        services.AddSingleton<IRouter>(sp => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(sp), sp));
        services.RegisterRoute<FirstView, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/first");
        services.RegisterRoute<SecondView, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/second");
        services.AddTransient<FakeViewModel>();
        services.AddTransient<SecondViewModel>();
        return services.BuildServiceProvider();
    }

    private static Task First(TestRenderer renderer, IServiceProvider provider, string region)
        => renderer.Dispatcher.InvokeAsync(() => provider.GoToAsync(
            new RouteableMenuEntry<FakeViewModel>("First", null, "/first", "/first") { Region = region }, TestContext.Current.CancellationToken));

    private static Task Second(TestRenderer renderer, IServiceProvider provider, string region)
        => renderer.Dispatcher.InvokeAsync(() => provider.GoToAsync(
            new RouteableMenuEntry<SecondViewModel>("Second", null, "/second", "/second") { Region = region }, TestContext.Current.CancellationToken));

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.True(condition());
    }

    private static bool AliveIs(params string[] names)
    {
        lock (Alive) return Alive.SequenceEqual(names);
    }

    [Fact]
    public async Task Every_view_of_the_region_is_shown_oldest_first_and_the_list_follows_pop()
    {
        using var provider = Build();
        var renderer = new TestRenderer(provider);
        await renderer.Render<RouteableRegionItems>(new() { ["Region"] = "a" });
        Assert.True(AliveIs());

        await First(renderer, provider, "a");
        await Until(() => AliveIs(nameof(FakeViewModel)));
        await Second(renderer, provider, "a");
        await Until(() => AliveIs(nameof(FakeViewModel), nameof(SecondViewModel)));

        await renderer.Dispatcher.InvokeAsync(() => provider.GetRouter("a").PopAsync(TestContext.Current.CancellationToken));
        await Until(() => AliveIs(nameof(FakeViewModel)));
    }

    [Fact]
    public async Task The_grid_takes_its_class_and_the_least_width_of_a_view()
    {
        using var provider = Build();
        var renderer = new TestRenderer(provider);
        await renderer.Render<RouteableRegionItems>(new() { ["Region"] = "a", ["MinItemWidth"] = 320, ["Class"] = "gallery" });

        Assert.Contains(renderer.Attributes, a => a is ("class", "gallery"));
        Assert.Contains(renderer.Attributes, a => a.Name == "style" && a.Value!.Contains("minmax(320px,1fr)"));

        await First(renderer, provider, "a");
        await Until(() => renderer.Attributes.Any(a => a.Name == "style" && a.Value == "min-width:0"));
    }

    [Fact]
    public async Task Naming_another_region_follows_the_views_of_that_one()
    {
        using var provider = Build();
        var renderer = new TestRenderer(provider);
        var id = await renderer.Render<RouteableRegionItems>(new() { ["Region"] = "a" });
        await First(renderer, provider, "a");
        await Until(() => AliveIs(nameof(FakeViewModel)));

        await renderer.Update(id, new() { ["Region"] = "b" });
        await Until(() => AliveIs());

        // The region it left no longer reaches it; the one it follows now does.
        await Second(renderer, provider, "a");
        await Second(renderer, provider, "b");
        await Until(() => AliveIs(nameof(SecondViewModel)));
        await renderer.Dispatcher.InvokeAsync(() => provider.GetRouter("a").PopAsync(TestContext.Current.CancellationToken));
        Assert.True(AliveIs(nameof(SecondViewModel)));
    }

    [Fact]
    public async Task A_component_that_is_gone_no_longer_follows_the_router()
    {
        using var provider = Build();
        var renderer = new TestRenderer(provider);
        await renderer.Render<RouteableRegionItems>(new() { ["Region"] = "a" });
        await First(renderer, provider, "a");
        await Until(() => AliveIs(nameof(FakeViewModel)));

        renderer.Dispose();
        await Until(() => AliveIs());

        await Second(renderer, provider, "a");
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(AliveIs());
    }
}
