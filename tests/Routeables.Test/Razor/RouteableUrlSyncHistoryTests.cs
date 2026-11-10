using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Abstraction.Registrations;
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

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DetailVm(int id) { public int Id { get; } = id; }

    public sealed class NamedView : IRouteableFor { public object? Context { get; set; } }
    public sealed class NamedVm(string name) { public string Name { get; } = name; }

    private sealed class Fixture : IDisposable
    {
        public ServiceProvider Provider { get; }
        public FakeNavigation Navigation { get; } = new();
        public FakeScripts Scripts { get; }
        public IRouter Router { get; }
        public RouteableUrlSync Sync { get; }

        public Fixture(bool withScripts = true)
        {
            Scripts = new FakeScripts(Navigation);
            var services = new ServiceCollection();
            services.RegisterRouting();
            services.RegisterRoute<HomeView, HomeVm>(route: "/home");
            services.RegisterRoute<DetailView, DetailVm>(route: "/detail/{id:int}");
            services.RegisterRoute<NamedView, NamedVm>(route: "/named/{name}");
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
        await f.GoAsync("/detail/1", "/detail/{id:int}");     // a push: a new browser entry
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);

        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.Empty(f.Navigation.Calls);                       // the address is not replaced: the browser moved itself
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
        Assert.Equal(entries, f.Router.History.Entries.Count);  // moved forward: no new entry was pushed
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
