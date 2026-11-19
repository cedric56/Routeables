using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Razor;

/// <summary>A Back or a Forward the router makes itself (<c>GoBackAsync</c> from a view model) is the same move in the history of the browser.</summary>
public class RouteableUrlSyncHistoryTests
{
    // A browser: the entries of its history, the one shown, and what history.go does.
    private sealed class FakeNavigation : NavigationManager
    {
        private readonly List<string> _entries = ["http://localhost/home"];
        private int _at;

        public List<(string Uri, bool Replace)> Calls { get; } = [];
        public FakeNavigation() => Initialize("http://localhost/", "http://localhost/home");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            var absolute = ToAbsoluteUri(uri).AbsoluteUri;      // escaped, as a browser reports it
            Calls.Add(("/" + ToBaseRelativePath(absolute), options.ReplaceHistoryEntry));
            if (options.ReplaceHistoryEntry)
                _entries[_at] = absolute;
            else
            {
                _entries.RemoveRange(_at + 1, _entries.Count - _at - 1);
                _entries.Add(absolute);
                _at++;
            }
            Uri = absolute;
            NotifyLocationChanged(isInterceptedLink: false);
        }

        // A link the user clicks: Blazor asks the handlers first (intercepted), then the browser pushes the entry.
        public async Task<bool> ClickAsync(string route) => await MoveAsync(route, intercepted: true);

        // The Back button of the browser to the entry before: the handlers are asked too, but it is not a link.
        public async Task<bool> BackButtonAsync()
        {
            var target = _entries[_at - 1];
            if (!await NotifyLocationChangingAsync(target, null, isNavigationIntercepted: false))
                return false;
            Go(-1);
            return true;
        }

        private async Task<bool> MoveAsync(string route, bool intercepted)
        {
            var absolute = ToAbsoluteUri(route).AbsoluteUri;
            if (!await NotifyLocationChangingAsync(absolute, null, intercepted))
                return false;
            _entries.RemoveRange(_at + 1, _entries.Count - _at - 1);
            _entries.Add(absolute);
            _at++;
            Uri = absolute;
            NotifyLocationChanged(isInterceptedLink: intercepted);
            return true;
        }

        public void Go(int delta)
        {
            _at = Math.Clamp(_at + delta, 0, _entries.Count - 1);
            Uri = _entries[_at];
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    // What the browser would be told: every history.go(delta), which moves the fake browser.
    private sealed class FakeScripts(FakeNavigation browser) : IJSRuntime
    {
        public List<int> Moves { get; } = [];
        public int ModulesDisposed { get; private set; }
        public bool Broken { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => identifier == "import"
                ? Broken ? throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.") : new((TValue)(object)new Module(this))
                : default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        private sealed class Module(FakeScripts owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                if (identifier == "go")
                {
                    var delta = (int)args![0]!;
                    owner.Moves.Add(delta);
                    owner.Browser.Go(delta);
                }
                return default;
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
                => InvokeAsync<TValue>(identifier, args);

            public ValueTask DisposeAsync() { owner.ModulesDisposed++; return default; }
        }

        public FakeNavigation Browser { get; } = browser;
    }

    public sealed class DetailVm(int id) { public int Id { get; } = id; }

    // A page that waits for ever to be entered, until its route change is cancelled.
    public sealed class SlowView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SlowVm : IRouteable
    {
        public static volatile bool Entering;
        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            Entering = true;
            return Task.Delay(Timeout.Infinite, ct);
        }
    }

    private sealed class Errors : IRouteableErrorSink
    {
        public List<Exception> Reported { get; } = [];
        public void Report(string source, Exception exception) { lock (Reported) Reported.Add(exception); }
    }

    private sealed class Fixture : IDisposable
    {
        public Errors Errors { get; } = new();
        public ServiceProvider Provider { get; }
        public FakeNavigation Navigation { get; } = new();
        public FakeScripts Scripts { get; }
        public IRouter Router { get; }
        public RouteableUrlSync Sync { get; }

        public Fixture(bool withScripts = true, bool cancelCurrent = false, bool notFound = false)
        {
            Scripts = new FakeScripts(Navigation);
            var services = new ServiceCollection();
            services.RegisterRouting();
            services.RegisterRoute<HomeView, HomeVm>(route: "/home");
            services.RegisterRoute<DetailView, DetailVm>(route: "/detail/{id:int}");
            services.RegisterRoute<NamedView, NamedVm>(route: "/named/{name}");
            services.RegisterRoute<RootView, RootVm>(route: "/");
            if (notFound)
            {
                services.RegisterRoute<NotFoundView, NotFoundVm>(route: "/not-found");
                services.UseNotFoundRoute();
            }
            services.RegisterRoute<SlowView, SlowVm>(route: "/slow");
            services.AddSingleton<IRouteableErrorSink>(Errors);
            if (cancelCurrent)
                services.SetConcurrency(RouterConcurrency.CancelCurrent);
            services.AddSingleton<NavigationManager>(Navigation);
            if (withScripts)
                services.AddSingleton<IJSRuntime>(Scripts);
            Provider = services.BuildServiceProvider();
            Router = Provider.GetRequiredService<IRouter>();
            Sync = new RouteableUrlSync(Navigation, Router, Provider);
        }

        public async Task GoAsync(string route, string template)
        {
            Assert.True((await Router.GoToAsync(route, template: template, cancellationToken: TestContext.Current.CancellationToken)).Success);
        }

        public static async Task Until(Func<bool> condition)
        {
            for (var i = 0; i < 200 && !condition(); i++)
                await Task.Delay(10);
            Assert.True(condition(), "condition not reached");
        }

        public void Dispose() { Sync.Dispose(); Provider.Dispose(); }
    }

    [Fact]
    public async Task A_back_of_the_router_goes_back_in_the_browser_history()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);

        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.Empty(f.Navigation.Calls);
    }

    [Fact]
    public async Task A_back_of_the_browser_that_a_newer_route_change_replaces_is_not_an_error()
    {
        using var f = new Fixture(cancelCurrent: true);
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        SlowVm.Entering = false;
        var slow = f.Router.GoToAsync("/slow", cancellationToken: TestContext.Current.CancellationToken);       // holds the router
        await Fixture.Until(() => SlowVm.Entering);

        f.Navigation.Go(-1);        // the browser goes back to /home: the router queues a Back behind the slow page
        var newer = await f.Router.GoToAsync("/detail/2", cancellationToken: TestContext.Current.CancellationToken);   // replaces it
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.True(newer.Success);
        Assert.DoesNotContain(f.Errors.Reported, e => e is OperationCanceledException);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slow);
    }

    [Fact]
    public async Task A_link_to_an_address_nobody_serves_shows_the_not_found_page_when_there_is_one()
    {
        using var f = new Fixture(notFound: true);
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");

        Assert.True(await f.Navigation.ClickAsync("/nope"));

        Assert.IsType<NotFoundVm>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task An_address_nobody_serves_changes_nothing_without_a_not_found_page()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");

        Assert.True(await f.Navigation.ClickAsync("/nope"));

        Assert.IsType<HomeVm>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task Spaces_around_an_address_are_not_part_of_the_route()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();

        Assert.True((await f.Router.GoToAsync("  /detail/7  ", cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.IsType<DetailVm>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task A_link_to_the_home_on_the_root_address_leaves_a_page_that_has_no_route()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        Assert.True((await f.Router.GoAsync(new OrphanView(), new OrphanVm(), cancellationToken: TestContext.Current.CancellationToken)).Success);   // shown under "/", like the home
        Assert.IsType<OrphanVm>(f.Router.CurrentViewModel);

        Assert.True(await f.Navigation.ClickAsync("/"));

        Assert.IsType<RootVm>(f.Router.CurrentViewModel);       // the link was taken for the page that is shown, and nothing moved
    }

    [Fact]
    public async Task A_link_to_the_page_before_is_a_new_page_for_the_router_as_it_is_for_the_browser()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        var before = f.Router.History.Count;

        Assert.True(await f.Navigation.ClickAsync("/home"));        // the browser pushed an entry for the link

        Assert.Equal(before + 1, f.Router.History.Count);
        Assert.Equal(before, f.Router.History.CurrentIndex);
        Assert.IsType<HomeVm>(f.Router.CurrentViewModel);

        // The two histories agree: a Back of the router is a Back of the browser, not a move of the router alone.
        f.Navigation.Calls.Clear();
        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);
        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.IsType<DetailVm>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task A_route_change_that_a_newer_one_replaces_is_not_an_error()
    {
        using var f = new Fixture(cancelCurrent: true);
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        SlowVm.Entering = false;

        var slow = f.Navigation.ClickAsync("/slow");                       // the page waits to be entered
        await Fixture.Until(() => SlowVm.Entering);
        await f.GoAsync("/detail/1", "/detail/{id:int}");                  // the application routes elsewhere: the router replaces the first
        await slow;

        Assert.Empty(f.Errors.Reported);
        Assert.IsType<DetailVm>(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task The_back_button_of_the_browser_still_goes_back_in_the_router()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        var before = f.Router.History.Count;

        Assert.True(await f.Navigation.BackButtonAsync());

        Assert.Equal(before, f.Router.History.Count);
        Assert.Equal(before - 2, f.Router.History.CurrentIndex);
        Assert.IsType<HomeVm>(f.Router.CurrentViewModel);
    }

    [Fact]
    public void An_escaped_slash_is_not_the_same_address_as_a_separator()
    {
        Assert.False(RouteableUrlSync.Same("/files/a%2Fb", "/files/a/b"));
        Assert.True(RouteableUrlSync.Same("/files/a%2Fb", "/files/a%2fb"));
        Assert.True(RouteableUrlSync.Same("/dogs/Afghan%20Hound", "/dogs/Afghan Hound"));
        Assert.True(RouteableUrlSync.Same("/dogs/Afghan%20Hound/", "/dogs/Afghan Hound"));
    }

    [Fact]
    public async Task Disposing_the_sync_lets_go_of_the_script_module()
    {
        var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);        // the module was imported to move the history

        await f.Sync.DisposeAsync();

        Assert.Equal(1, f.Scripts.ModulesDisposed);
        f.Provider.Dispose();
    }

    [Fact]
    public async Task The_browser_forward_to_an_address_with_a_space_goes_forward_in_the_router()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/named/Afghan Hound", "/named/{name}");
        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);
        var entries = f.Router.History.Entries.Count;

        f.Navigation.Go(1);                                     // the Forward button: the address is "/named/Afghan%20Hound"
        await Fixture.Until(() => f.Router.CurrentViewModel is NamedVm);

        Assert.Equal("Afghan Hound", ((NamedVm)f.Router.CurrentViewModel!).Name);
        Assert.Equal(entries, f.Router.History.Entries.Count);
    }

    [Fact]
    public async Task A_pop_of_the_router_goes_back_in_the_browser_history_and_forward_opens_the_page_again()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/named/Afghan Hound", "/named/{name}");
        f.Navigation.Calls.Clear();

        await f.Router.PopAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);

        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.Empty(f.Navigation.Calls);                       // not replaced: the browser went back, it does not stay on a copy of the list
        Assert.IsType<HomeVm>(f.Router.CurrentViewModel);
        var entries = f.Router.History.Entries.Count;

        f.Navigation.Go(1);                                     // the Forward button: the page the router forgot is opened again
        await Fixture.Until(() => f.Router.CurrentViewModel is NamedVm);

        Assert.Equal("Afghan Hound", ((NamedVm)f.Router.CurrentViewModel!).Name);
        Assert.Equal(entries + 1, f.Router.History.Entries.Count);
    }

    [Fact]
    public async Task A_forward_of_the_router_goes_forward_in_the_browser_history()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);

        await f.Router.ForwardAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 2);

        Assert.Equal(2, f.Scripts.Moves.Count);
        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.Equal(1, f.Scripts.Moves[1]);
    }

    [Fact]
    public async Task After_a_move_to_an_entry_the_browser_is_no_longer_followed_and_the_address_is_replaced()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        await f.GoAsync("/detail/2", "/detail/{id:int}");
        await f.Router.MoveToAsync(0, TestContext.Current.CancellationToken);   // the router history no longer follows the browser's
        f.Navigation.Calls.Clear();
        f.Scripts.Moves.Clear();

        await f.Router.ForwardAsync(TestContext.Current.CancellationToken);

        Assert.Empty(f.Scripts.Moves);
        Assert.Equal(("/detail/1", true), f.Navigation.Calls.Last());          // replaced, not history.go
    }

    [Fact]
    public async Task Without_scripts_the_address_is_replaced()
    {
        using var f = new Fixture(withScripts: false);
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);

        Assert.Single(f.Navigation.Calls);
        Assert.Equal(("/home", true), f.Navigation.Calls[0]);
    }

    [Fact]
    public async Task When_the_script_cannot_run_the_address_is_replaced()
    {
        using var f = new Fixture();
        f.Scripts.Broken = true;
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Navigation.Calls.Count == 1);

        Assert.Equal(("/home", true), f.Navigation.Calls[0]);
    }
}
