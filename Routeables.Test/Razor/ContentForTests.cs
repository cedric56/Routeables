using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routeables.Razor;

namespace Routeables.Tests.Razor;

public class ContentForTests
{
    private sealed class ChildComponent : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public string? Title { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Title);
            builder.AddContent(1, ChildContent);
        }
    }

    private sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, Content);
    }

    [Fact]
    public async Task Render_forwards_child_content_and_component_parameters()
    {
        var content = new RouteableContentFor<ChildComponent>(
            "dialog body",
            new Dictionary<string, object> { ["Title"] = "Dialog title" });
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        using var provider = serviceCollection.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<FragmentHost>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(FragmentHost.Content)] = content.Render() }));
            return rendered.ToHtmlString();
        });

        Assert.Equal("Dialog titledialog body", html);
    }
}
