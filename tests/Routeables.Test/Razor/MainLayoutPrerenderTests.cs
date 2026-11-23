using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Blazor.Components;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Core;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Razor;

/// <summary>
/// <see cref="RouteableMainLayoutBase"/> while the server prerenders the page: the application asked for it with <see cref="RouteablePrerenderOptions"/>,
/// the page is chosen when the layout is initialized (the first render never comes) and the HTML holds it.
/// </summary>
public class MainLayoutPrerenderTests
{
    private sealed class Layout : RouteableMainLayoutBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
        }
    }

    private sealed class Navigation(string address) : NavigationManager
    {
        public void Start() => Initialize("http://localhost/", address);

        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HtmlRenderer renderer;

        public List<IServiceProvider> Initialized { get; } = [];

        public IRouter Router => provider.GetRequiredService<IRouter>();

        public Fixture(string address, RouteablePrerenderOptions? options = null, bool persistence = false, bool menu = true)
        {
            var navigation = new Navigation("http://localhost" + address);
            navigation.Start();
            var menuViewModel = new FakeMenuViewModel();
            if (menu)
            {
                menuViewModel.Entries.Add(new RouteableMenuEntry<FakeViewModel>("First", null, "/first", "/first"));
                menuViewModel.Entries.Add(new RouteableMenuEntry<SecondViewModel>("Second", null, "/second", "/second"));
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.RegisterRouting();
            services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/first");
            services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/second");
            services.AddTransient<FakeViewModel>();
            services.AddTransient<SecondViewModel>();
            services.AddSingleton<NavigationManager>(navigation);
            services.AddSingleton<IRouteableRequestedThemeService>(new RouteableRequestThemeService());
            services.AddSingleton<IRouteableMenuViewModel>(menuViewModel);
            services.UseRouteablesServices();
            services.AddSingleton(Substitute.For<IJSRuntime>());
            services.AddSingleton<Func<IServiceProvider, Task>>(sp => p => { Initialized.Add(p); return Task.CompletedTask; });
            services.AddSingleton<RouteableUrlSync>();
            services.AddSingleton(options ?? new RouteablePrerenderOptions());
            if (persistence)
                services.AddSingleton(new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance).State);
            provider = services.BuildServiceProvider();
            renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        public Task RenderAsync() => renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Layout>());

        /// <summary>The HTML of the layout of the package itself (<see cref="RouteableMainLayout"/>), with the parameters given.</summary>
        public Task<string> RenderMainLayoutAsync(Dictionary<string, object?>? parameters = null)
            => renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<RouteableMainLayout>(ParameterView.FromDictionary(parameters ?? []))).ToHtmlString());

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_page_of_the_address_is_chosen_while_the_layout_is_initialized()
    {
        await using var f = new Fixture("/second");

        await f.RenderAsync();

        Assert.IsType<SecondViewModel>(f.Router.CurrentViewModel);
        Assert.Single(f.Initialized);
    }

    [Fact]
    public async Task An_address_that_matches_nothing_shows_the_first_entry_of_the_menu_by_default()
    {
        await using var f = new Fixture("/nowhere");

        await f.RenderAsync();

        Assert.IsType<FakeViewModel>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task The_root_address_is_the_default_page_even_when_an_unknown_address_is_not_found()
    {
        await using var f = new Fixture("/", new RouteablePrerenderOptions { UnknownAddressIsNotFound = true });

        await f.RenderAsync();

        Assert.IsType<FakeViewModel>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task An_unknown_address_is_not_found_when_the_application_asks_for_it()
    {
        await using var f = new Fixture("/nowhere", new RouteablePrerenderOptions { UnknownAddressIsNotFound = true });

        await f.RenderAsync();

        // No default page: the response is a 404 (here there is no response to give a status to, and the layout carries on).
        Assert.Null(f.Router.CurrentViewModel);
        Assert.Single(f.Initialized);
    }

    [Fact]
    public async Task The_history_is_kept_for_the_circuit_that_takes_over_when_a_state_is_there()
    {
        await using var f = new Fixture("/first", persistence: true);

        await f.RenderAsync();

        Assert.IsType<FakeViewModel>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task The_layout_of_the_package_draws_the_bar_the_menu_and_the_hosts_when_the_menu_has_entries()
    {
        await using var f = new Fixture("/first");

        var html = await f.RenderMainLayoutAsync();

        Assert.Contains("rt-layout", html);
        Assert.DoesNotContain("rt-no-menu", html);
        Assert.Contains("rt-menu-button", html);
        Assert.Contains("rt-drawer", html);
        Assert.Contains("Second", html);
        Assert.Contains("rt-content", html);
    }

    [Fact]
    public async Task The_layout_of_the_package_has_no_drawer_and_no_button_to_open_it_without_a_menu()
    {
        await using var f = new Fixture("/first", menu: false);

        var html = await f.RenderMainLayoutAsync();

        Assert.Contains("rt-no-menu", html);
        Assert.DoesNotContain("rt-drawer", html);
        Assert.DoesNotContain("aria-label=\"Toggle menu\"", html);
        Assert.Contains("rt-theme-button", html);
    }

    [Fact]
    public async Task The_layout_of_the_package_shows_the_content_it_is_given_instead_of_its_own_frame()
    {
        await using var f = new Fixture("/first");
        RenderFragment content = builder => builder.AddMarkupContent(0, "<p id=\"mine\">my layout</p>");

        var html = await f.RenderMainLayoutAsync(new() { [nameof(RouteableMainLayout.ChildContent)] = content });

        Assert.Contains("my layout", html);
        Assert.DoesNotContain("rt-layout", html);
    }
}
