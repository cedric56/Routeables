// The test renderer is a Renderer: it has to read the render tree frames, which the analyzer discourages outside the framework.
#pragma warning disable BL0006
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Routing;
using Dispatcher = Microsoft.AspNetCore.Components.Dispatcher;

namespace Routeables.Tests.Razor;

/// <summary>The component of a page that receives a region renders what the router of that region displays, and follows it.</summary>
public class RouteableRegionTests
{
    private sealed class Hello : ComponentBase
    {
        [Parameter] public object? Context { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "hello-" + Context?.GetType().Name);
    }

    private sealed class FirstView : RouteableFor { protected override void OpenComponent(RenderTreeBuilder builder) => builder.OpenComponent<Hello>(0); }
    private sealed class SecondView : RouteableFor { protected override void OpenComponent(RenderTreeBuilder builder) => builder.OpenComponent<Hello>(0); }
    private sealed class FirstViewModel;
    private sealed class SecondViewModel;

    private sealed class TestRenderer(IServiceProvider provider) : Renderer(provider, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public List<string> Texts { get; } = [];

        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            for (var i = 0; i < batch.ReferenceFrames.Count; i++)
            {
                var frame = batch.ReferenceFrames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Text)
                    Texts.Add(frame.TextContent);
            }

            return Task.CompletedTask;
        }

        protected override void HandleException(Exception exception) => throw exception;

        public Task Render<T>(Dictionary<string, object?> parameters) where T : IComponent
        {
            var id = AssignRootComponentId(InstantiateComponent(typeof(T)));
            return Dispatcher.InvokeAsync(() => RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters)));
        }
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddRegion("detail");
        services.AddSingleton<IRouter>(sp => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(sp), sp));
        services.RegisterRoute<FirstView, FirstViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/first");
        services.RegisterRoute<SecondView, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/second");
        services.AddTransient<FirstViewModel>();
        services.AddTransient<SecondViewModel>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_component_renders_the_view_the_entry_sends_to_its_region()
    {
        using var provider = Build();
        var renderer = new TestRenderer(provider);
        await renderer.Render<RouteableRegion>(new() { ["Region"] = "detail" });
        Assert.Empty(renderer.Texts);

        var first = new RouteableEntry<FirstViewModel>("First", null, "/first", "/first") { Region = "detail" };
        await renderer.Dispatcher.InvokeAsync(() => provider.GoToAsync(first, TestContext.Current.CancellationToken));
        await WaitFor(renderer, "hello-FirstViewModel");

        renderer.Texts.Clear();
        var second = new RouteableEntry<SecondViewModel>("Second", null, "/second", "/second") { Region = "detail" };
        await renderer.Dispatcher.InvokeAsync(() => provider.GoToAsync(second, TestContext.Current.CancellationToken));
        await WaitFor(renderer, "hello-SecondViewModel");

        // A component that is gone is no longer rendered by the router.
        renderer.Dispose();
        await provider.GetRouter("detail").EraseHistoryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitFor(TestRenderer renderer, string text)
    {
        for (var i = 0; i < 100 && !renderer.Texts.Contains(text); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Contains(text, renderer.Texts);
    }
}
