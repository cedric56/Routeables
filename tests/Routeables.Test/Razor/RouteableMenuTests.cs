using Microsoft.AspNetCore.Components;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Icons;
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

    /// <summary>A menu that stays on the screen: it is rendered once, then the menu view model changes.</summary>
    private sealed class Shown(FakeMenuViewModel main) : IAsyncDisposable
    {
        private readonly ServiceProvider _provider = Build(main);
        private Microsoft.AspNetCore.Components.Web.HtmlRenderer? _renderer;
        private Func<string>? _toHtml;

        private static ServiceProvider Build(FakeMenuViewModel main)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IRouteableMenuViewModel>(main);
            return services.BuildServiceProvider();
        }

        public async Task StartAsync()
        {
            _renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(_provider, _provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
            var root = await _renderer.Dispatcher.InvokeAsync(() => _renderer.RenderComponentAsync<RouteableMenu>());
            _toHtml = () => root.ToHtmlString();
        }

        public Task<string> HtmlAsync() => _renderer!.Dispatcher.InvokeAsync(() => _toHtml!());

        public async ValueTask StopAsync() => await _renderer!.DisposeAsync();

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task An_entry_added_after_the_render_appears_in_the_menu()
    {
        var main = Menu();
        await using var shown = new Shown(main);
        await shown.StartAsync();

        main.Entries.Add(new RouteableMenuEntry<Home>("Contact", null, "/contact", "/contact"));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Contains("Contact", await shown.HtmlAsync());
    }

    [Fact]
    public async Task A_menu_that_is_locked_after_the_render_disables_its_entries()
    {
        var main = Menu();
        await using var shown = new Shown(main);
        await shown.StartAsync();
        Assert.DoesNotContain("disabled", await shown.HtmlAsync());

        main.Entries[0].IsLock = true;
        main.NotifyLockChanged();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Contains("disabled", await shown.HtmlAsync());
    }

    [Fact]
    public async Task The_menu_stops_following_the_menu_view_model_when_it_is_disposed()
    {
        var main = Menu();
        await using var shown = new Shown(main);
        await shown.StartAsync();
        Assert.Equal(1, main.LockSubscribers);

        await shown.StopAsync();

        Assert.Equal(0, main.LockSubscribers);
        main.NotifyLockChanged(); // nothing is left to refresh, and nothing throws
    }

    private sealed class Glyphs(string key, object? glyph) : IRouteableIconResolver
    {
        public object? GetIcon(string requested) => requested == key ? glyph : null;
        public object? GetSource(string requested) => null;
    }

    [Fact]
    public async Task An_entry_whose_icon_is_a_text_shows_it_and_any_other_icon_is_left_to_the_host()
    {
        var main = new FakeMenuViewModel();
        var text = new RouteableIconResolverRegistry();
        text.Register(new Glyphs("text", "GLYPH-A"));
        var image = new RouteableIconResolverRegistry();
        image.Register(new Glyphs("image", new object()));
        main.Entries.Add(new RouteableMenuEntry("/a", "/a", "WithText", "text") { Icons = text });
        main.Entries.Add(new RouteableMenuEntry("/b", "/b", "WithImage", "image") { Icons = image });
        main.Entries.Add(new RouteableMenuEntry("/c", "/c", "WithNone"));

        var html = await RenderAsync(main);

        Assert.Contains("GLYPH-A", html);
        Assert.Single(html.Split("rt-nav-icon").Skip(1));
        Assert.Contains("WithImage", html);
        Assert.Contains("WithNone", html);
    }

    [Fact]
    public async Task The_menu_is_named_Menu_unless_another_name_is_given()
    {
        Assert.Contains("aria-label=\"Menu\"", await RenderAsync(Menu()));
        Assert.Contains("aria-label=\"Main navigation\"", await RenderAsync(Menu(), new() { [nameof(RouteableMenu.Label)] = "Main navigation" }));
    }

    [Fact]
    public async Task A_menu_marks_the_new_current_entry_when_the_view_changes()
    {
        var main = Menu();
        await using var shown = new Shown(main);
        await shown.StartAsync();
        Assert.Contains("rt-nav-link rt-active\"", await shown.HtmlAsync());
        Assert.Equal(1, main.ViewSubscribers);

        main.SelectedEntry = main.Entries[0];
        main.NotifyViewChanged(new object());
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var html = await shown.HtmlAsync();
        Assert.True(html.IndexOf("rt-active", StringComparison.Ordinal) < html.IndexOf("About", StringComparison.Ordinal),
            "the first entry (Home) is the active one now");
        Assert.Single(html.Split("aria-current=\"page\"").Skip(1));

        await shown.StopAsync();
        Assert.Equal(0, main.ViewSubscribers);
    }
}
