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

        public Fixture(string address, RouteablePrerenderOptions? options = null, bool persistence = false)
        {
            var navigation = new Navigation("http://localhost" + address);
            navigation.Start();
            var menu = new FakeMenuViewModel();
            menu.Entries.Add(new RouteableMenuEntry<FakeViewModel>("First", null, "/first", "/first"));
            menu.Entries.Add(new RouteableMenuEntry<SecondViewModel>("Second", null, "/second", "/second"));

            var services = new ServiceCollection();
            services.AddLogging();
            services.RegisterRouting();
            services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/first");
            services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/second");
            services.AddTransient<FakeViewModel>();
            services.AddTransient<SecondViewModel>();
            services.AddSingleton<NavigationManager>(navigation);
            services.AddSingleton<IRouteableRequestedThemeService>(new RouteableRequestThemeService());
            services.AddSingleton<IRouteableMenuViewModel>(menu);
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
}
