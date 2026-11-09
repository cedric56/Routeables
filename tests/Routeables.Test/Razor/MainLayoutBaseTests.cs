using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Services;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Razor;

/// <summary>
/// <see cref="RouteableMainLayoutBase"/>: what every layout gets without any markup. The first render starts Routeables (address, initialization,
/// theme, first page), the title and the menu follow the main view model, and the events are let go when the layout goes.
/// </summary>
public class MainLayoutBaseTests
{
    /// <summary>Hands the test the layout the renderer made: the static renderer never raises the events after a render, the test does.</summary>
    private sealed class Probe
    {
        public Layout? Layout { get; set; }
    }

    private sealed class Layout : RouteableMainLayoutBase
    {
        [Inject] public Probe Instances { get; set; } = default!;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Instances.Layout = this;
        }

        /// <summary>What the browser does after the first render: HtmlRenderer is static and never calls OnAfterRenderAsync.</summary>
        public Task FirstRenderAsync() => OnAfterRenderAsync(true);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "main");
            builder.AddAttribute(1, "data-title", Title);
            builder.AddAttribute(2, "data-theme", ThemeName);
            builder.CloseElement();
        }

        public void Attach(IMainViewModel menu, IRouteableRequestedThemeService theme)
        {
            MainViewModel = menu;
            ThemeService = theme;
        }

        public void Toggle() => ToggleTheme();

        public bool DrawerOpen => _drawerOpen;

        public void ToggleDrawer() => DrawerToggle();
    }

    private sealed class Menu : IMainViewModel
    {
        public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];
        public IRouteableMenuEntry? SelectedEntry { get; set; }
        public object? CurrentView { get; set; }
        public string? Title { get; set; }
        public string? CurrentTitle => Title ?? SelectedEntry?.DisplayName;

        public event EventHandler<object?>? CurrentViewChanged;
        public event EventHandler? LockChanged;

        public void Raise(object? view) => CurrentViewChanged?.Invoke(this, view);
        public void RaiseLock() => LockChanged?.Invoke(this, EventArgs.Empty);
        public int ViewSubscribers => CurrentViewChanged?.GetInvocationList().Length ?? 0;
        public int LockSubscribers => LockChanged?.GetInvocationList().Length ?? 0;
    }

    private sealed class RecordingJs(Exception? failure = null, int failAfter = 0) : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            if (failure is not null && Calls.Count > failAfter) throw failure;
            return new(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HtmlRenderer renderer;
        private HtmlRootComponent? root;

        private Probe Instances { get; } = new();

        public Menu Menu { get; } = new();
        public RouteableRequestThemeService Theme { get; } = new() { IsDarkMode = false };
        public RecordingJs Js { get; }
        public List<IServiceProvider> Initialized { get; } = [];

        public Fixture(Exception? jsFailure = null, int jsFailAfter = 0)
        {
            Js = new RecordingJs(jsFailure, jsFailAfter);
            var services = new ServiceCollection();
            services.AddLogging();
            services.RegisterRouting();
            services.AddSingleton<NavigationManager>(new Navigation());
            services.AddSingleton<IRouteableRequestedThemeService>(Theme);
            services.AddSingleton<IMainViewModel>(Menu);
            services.AddSingleton<IJSRuntime>(Js);
            services.AddSingleton<Func<IServiceProvider, Task>>(sp => p => { Initialized.Add(p); return Task.CompletedTask; });
            services.AddSingleton<RouteableUrlSync>();
            services.AddSingleton(Instances);
            provider = services.BuildServiceProvider();
            renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        public async Task StartAsync()
        {
            root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Layout>());
            await renderer.Dispatcher.InvokeAsync(() => Instances.Layout!.FirstRenderAsync());
        }

        public Task<string> HtmlAsync() => renderer.Dispatcher.InvokeAsync(() => root!.Value.ToHtmlString());

        public async Task UntilHtmlAsync(Func<string, bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                var html = await HtmlAsync();
                if (condition(html)) return;
                if (DateTime.UtcNow > deadline) throw new TimeoutException($"The markup never became what was expected: {html}");
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        public static async Task UntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private static IRouteableMenuEntry Entry(string name)
    {
        var entry = Substitute.For<IRouteableMenuEntry>();
        entry.DisplayName.Returns(name);
        // A route of its own: an entry whose route is empty would match the address "/" and be selected by the address.
        entry.Route.Returns("/" + name.ToLowerInvariant());
        return entry;
    }

    // ------------------------------------------------------------------------------------------------ the first render

    [Fact]
    public async Task The_first_render_starts_Routeables_for_the_circuit_once()
    {
        await using var f = new Fixture();

        await f.StartAsync();

        await Fixture.UntilAsync(() => f.Initialized.Count == 1);
        await f.HtmlAsync();
        Assert.Single(f.Initialized);
    }

    [Fact]
    public async Task The_first_render_applies_the_theme_to_the_document()
    {
        await using var f = new Fixture();

        await f.StartAsync();

        await Fixture.UntilAsync(() => f.Js.Calls.Count > 0);
        var call = f.Js.Calls[0];
        Assert.Equal("document.documentElement.setAttribute", call.Identifier);
        Assert.Equal(new object?[] { "data-wui-theme", "light" }, call.Args);
    }

    [Fact]
    public async Task The_dark_theme_is_applied_when_it_is_the_one_shown()
    {
        await using var f = new Fixture();
        f.Theme.IsDarkMode = true;

        await f.StartAsync();

        await Fixture.UntilAsync(() => f.Js.Calls.Count > 0);
        Assert.Equal(new object?[] { "data-wui-theme", "dark" }, f.Js.Calls[0].Args);
    }

    [Fact]
    public async Task A_disconnected_circuit_does_not_break_the_first_render()
    {
        await using var f = new Fixture(new JSDisconnectedException("gone"));

        await f.StartAsync();

        await Fixture.UntilAsync(() => f.Js.Calls.Count > 0);
        Assert.Contains("data-title=\"Application\"", await f.HtmlAsync());
    }

    [Fact]
    public async Task The_first_page_is_selected_when_the_address_chose_none()
    {
        await using var f = new Fixture();
        var first = Entry("First");
        f.Menu.Entries.Add(first);
        f.Menu.Entries.Add(Entry("Second"));

        await f.StartAsync();

        await Fixture.UntilAsync(() => f.Menu.SelectedEntry is not null);
        Assert.Same(first, f.Menu.SelectedEntry);
    }

    [Fact]
    public async Task The_menu_selection_is_kept_when_one_was_already_made()
    {
        await using var f = new Fixture();
        var chosen = Entry("Chosen");
        f.Menu.Entries.Add(Entry("First"));
        f.Menu.SelectedEntry = chosen;

        await f.StartAsync();

        await Fixture.UntilAsync(() => f.Js.Calls.Count > 0);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Same(chosen, f.Menu.SelectedEntry);
    }

    // ------------------------------------------------------------------------------------------------ title, menu, theme

    [Fact]
    public async Task The_title_is_Application_until_a_view_comes()
    {
        await using var f = new Fixture();

        await f.StartAsync();

        Assert.Contains("data-title=\"Application\"", await f.HtmlAsync());
    }

    [Fact]
    public async Task The_title_follows_the_current_view_of_the_main_view_model()
    {
        await using var f = new Fixture();
        await f.StartAsync();

        f.Menu.Title = "Orders";
        f.Menu.Raise(new object());

        await f.UntilHtmlAsync(h => h.Contains("data-title=\"Orders\""));
    }

    [Fact]
    public async Task A_view_without_a_title_brings_the_title_back_to_Application()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        f.Menu.Title = "Orders";
        f.Menu.Raise(new object());
        await f.UntilHtmlAsync(h => h.Contains("Orders"));

        f.Menu.Title = null;
        f.Menu.Raise(new object());

        await f.UntilHtmlAsync(h => h.Contains("data-title=\"Application\""));
    }

    [Fact]
    public async Task A_click_in_the_menu_selects_the_entry()
    {
        await using var f = new Fixture();
        var entry = Entry("Orders");
        var layout = new Layout();
        layout.Attach(f.Menu, f.Theme);

        layout.OnNavClick(entry);

        Assert.Same(entry, f.Menu.SelectedEntry);
    }

    [Fact]
    public async Task The_theme_button_asks_for_the_other_theme()
    {
        await using var f = new Fixture();
        var layout = new Layout();
        layout.Attach(f.Menu, f.Theme);
        Assert.False(f.Theme.IsDarkMode);

        layout.Toggle();
        Assert.True(f.Theme.IsDarkMode);

        layout.Toggle();
        Assert.False(f.Theme.IsDarkMode);
    }

    [Fact]
    public void The_drawer_starts_open_and_the_button_toggles_it()
    {
        var layout = new Layout();
        Assert.True(layout.DrawerOpen);

        layout.ToggleDrawer();
        Assert.False(layout.DrawerOpen);

        layout.ToggleDrawer();
        Assert.True(layout.DrawerOpen);
    }

    [Fact]
    public async Task A_theme_request_applies_the_new_theme_and_draws_the_layout_again()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        await Fixture.UntilAsync(() => f.Js.Calls.Count == 1);

        f.Theme.Request(RouteableRequestedTheme.Dark);

        await Fixture.UntilAsync(() => f.Js.Calls.Count == 2);
        Assert.Equal(new object?[] { "data-wui-theme", "dark" }, f.Js.Calls[1].Args);
        await f.UntilHtmlAsync(h => h.Contains("data-theme=\"dark\""));
    }

    [Fact]
    public async Task A_theme_that_cannot_be_applied_does_not_break_the_layout()
    {
        await using var f = new Fixture(new InvalidOperationException("js failed"), jsFailAfter: 1);
        await f.StartAsync();

        f.Theme.Request(RouteableRequestedTheme.Dark);

        await f.UntilHtmlAsync(h => h.Contains("data-theme=\"dark\""));
    }

    [Fact]
    public async Task A_lock_change_and_a_new_menu_entry_draw_the_layout_again_without_failing()
    {
        await using var f = new Fixture();
        await f.StartAsync();

        f.Menu.RaiseLock();
        f.Menu.Entries.Add(Entry("Added later"));

        Assert.Contains("<main", await f.HtmlAsync());
    }

    [Fact]
    public async Task The_events_are_let_go_when_the_layout_is_disposed()
    {
        var f = new Fixture();
        await f.StartAsync();
        Assert.True(f.Menu.ViewSubscribers > 0);
        Assert.True(f.Menu.LockSubscribers > 0);

        await f.DisposeAsync();

        Assert.Equal(0, f.Menu.ViewSubscribers);
        Assert.Equal(0, f.Menu.LockSubscribers);
    }

    [Fact]
    public async Task A_layout_removed_between_an_event_and_the_render_is_not_an_error()
    {
        var f = new Fixture();
        await f.StartAsync();
        await f.DisposeAsync();

        // The handlers are async void: nothing may throw to the caller, even after the renderer is gone.
        f.Menu.Raise(new object());
        f.Menu.RaiseLock();
        f.Theme.Request(RouteableRequestedTheme.Dark);
    }
}
