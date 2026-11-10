using Microsoft.AspNetCore.Components;
using Routeables.Abstraction.Bases;
using Routeables.Blazor.Components;
using Routeables.Routing;
using Routeables.Tests.Core;

namespace Routeables.Tests.Razor;

/// <summary><see cref="RouteableMenu"/>: one button per entry of the menu view model, the current one marked, the locked ones disabled.</summary>
public class RouteableMenuTests
{
    private sealed class Home;
    private sealed class About;

    private static async Task<string> RenderAsync(FakeMenuViewModel main, Dictionary<string, object?>? parameters = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRouteableMenuViewModel>(main);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<RouteableMenu>(ParameterView.FromDictionary(parameters ?? []))).ToHtmlString());
    }

    private static FakeMenuViewModel Menu()
    {
        var main = new FakeMenuViewModel();
        main.Entries.Add(new RouteableMenuEntry<Home>("Home", null, "/home", "/home", "The first page"));
        main.Entries.Add(new RouteableMenuEntry<About>("About", null, "/about", "/about"));
        main.SelectedEntry = main.Entries[1];
        return main;
    }

    [Fact]
    public async Task It_draws_a_button_for_each_entry_and_marks_the_current_one()
    {
        var html = await RenderAsync(Menu());

        Assert.Contains("<nav", html);
        Assert.Contains("Home", html);
        Assert.Contains("The first page", html);
        Assert.Contains("About", html);
        Assert.Equal(2, html.Split("rt-nav-link").Length - 1);
        Assert.Single(html.Split("aria-current=\"page\"").Skip(1));
        Assert.Contains("rt-nav-link rt-active", html);
    }

    [Fact]
    public async Task It_draws_nothing_without_entries()
    {
        var html = await RenderAsync(new FakeMenuViewModel());

        Assert.DoesNotContain("<nav", html);
    }

    [Fact]
    public async Task A_locked_entry_is_disabled_and_a_closed_menu_says_so()
    {
        var main = Menu();
        main.Entries[0].IsLock = true;

        var html = await RenderAsync(main, new() { [nameof(RouteableMenu.Closed)] = true, [nameof(RouteableMenu.Class)] = "side" });

        Assert.Contains("disabled", html);
        Assert.Contains("rt-drawer-closed", html);
        Assert.Contains("side", html);
    }
}
