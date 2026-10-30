using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor.Components;
using Routeables.Tests.Core;

namespace Routeables.Tests.Razor;

public class RouteableFormTests
{
    private sealed class FakeView(string name, bool locked = false) : IRouteableEntry
    {
        public bool IsLock { get; set; } = locked;
        public object? ViewModel { get; set; }
        public object? View { get; set; }
        public string Route => "/" + name;
        public string Template => "/" + name;
        public IRouteableArgs Arguments { get; set; } = null!;
        public string DisplayName => name;
        public string? Path => null;
    }

    private static RouteableViewModelBase CreateViewModel(params IRouteableEntry[] views) =>
        TestNavigableViewModel.Create(
            NSubstitute.Substitute.For<IRouter>(),
            new System.Collections.ObjectModel.ObservableCollection<IRouteableEntry>(views),
            initialize: false);

    private static async Task<string> RenderAsync(RouteableViewModelBase vm, bool busy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<RouteableForm>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(RouteableForm.MainViewModel)] = vm,
                [nameof(RouteableForm.IsBusy)] = busy,
                [nameof(RouteableForm.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "HEADER")),
            }));
            return root.ToHtmlString();
        });
    }

    [Fact]
    public async Task Renders_a_button_per_page_with_the_active_and_locked_states()
    {
        var vm = CreateViewModel(new FakeView("Home"), new FakeView("Settings", locked: true));

        var html = await RenderAsync(vm, busy: false);

        Assert.Contains("Home", html);
        Assert.Contains("Settings", html);
        Assert.Contains("disabled", html);
        Assert.Contains("HEADER", html);
        Assert.DoesNotContain("mud-", html);
        Assert.DoesNotContain("onclick=", html);
    }

    [Fact]
    public async Task The_busy_overlay_is_only_rendered_while_busy()
    {
        var vm = CreateViewModel();

        Assert.Contains("split-busy", await RenderAsync(vm, busy: true));
        Assert.DoesNotContain("split-busy", await RenderAsync(vm, busy: false));
    }
}
