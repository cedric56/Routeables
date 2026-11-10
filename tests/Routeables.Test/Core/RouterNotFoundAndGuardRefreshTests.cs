using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>UseNotFoundRoute (a page for addresses nobody serves) and RefreshGuardsAsync (the guards run again for the page shown).</summary>
public class RouterNotFoundAndGuardRefreshTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class MissingView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MissingVm;
    public sealed class LoginView : IRouteableFor { public object? Context { get; set; } }
    public sealed class LoginVm;

    private sealed class Session : INotifyPropertyChanged
    {
        private bool _signedIn = true;
        public bool IsSignedIn
        {
            get => _signedIn;
            set { _signedIn = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSignedIn))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Other() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Other"));
    }

    private sealed class NeedsSession(Session session, List<string> log) : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
        {
            log.Add($"{change.Kind}:{change.To.Args.Route}");
            var route = change.To.Args.Route?.Trim('/');
            return Task.FromResult(route == "login" || session.IsSignedIn ? RouteGuardDecision.Allow : RouteGuardDecision.Redirect("/login"));
        }
    }

    private static ServiceProvider Build(bool notFound, Session? session = null, List<string>? log = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<MissingView, MissingVm>(route: "/not-found");
        services.RegisterRoute<LoginView, LoginVm>(route: "/login");
        if (notFound) services.UseNotFoundRoute();
        if (session is not null)
        {
            services.AddSingleton(session);
            services.AddSingleton(log ?? []);
            services.AddSingleton<IRouteGuard, NeedsSession>();
        }
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Without_UseNotFoundRoute_an_unknown_address_is_a_failed_result()
    {
        await using var provider = Build(notFound: false);
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/nope", cancellationToken: Ct);

        Assert.False(result.Success);
        Assert.Null(router.CurrentViewModel);
    }

    [Fact]
    public async Task With_UseNotFoundRoute_the_page_is_shown_with_the_address_that_was_asked_for()
    {
        await using var provider = Build(notFound: true);
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/nope?x=1", cancellationToken: Ct);

        Assert.True(result.Success);
        Assert.IsType<MissingVm>(router.CurrentViewModel);
        var asked = router.History.Current!.Args.Values.Single(v => v.name == RouteableNotFound.UrlValue).value;
        Assert.Equal("/nope?x=1", asked);
    }

    [Fact]
    public async Task A_not_found_page_that_is_not_registered_does_not_loop()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.UseNotFoundRoute("/gone");
        await using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IRouter>().GoToAsync("/nope", cancellationToken: Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshGuardsAsync_redirects_when_the_page_shown_is_no_longer_allowed()
    {
        var session = new Session();
        var log = new List<string>();
        await using var provider = Build(notFound: false, session, log);
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        Assert.IsType<HomeVm>(router.CurrentViewModel);

        Assert.True((await router.RefreshGuardsAsync(Ct)).Success);          // still signed in: nothing changes
        Assert.IsType<HomeVm>(router.CurrentViewModel);

        session.IsSignedIn = false;
        var result = await router.RefreshGuardsAsync(Ct);

        Assert.True(result.Success);
        Assert.IsType<LoginVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task RefreshGuardsOn_follows_the_property_and_stops_when_disposed()
    {
        var session = new Session();
        await using var provider = Build(notFound: false, session);
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);

        var subscription = router.RefreshGuardsOn(session, null, nameof(Session.IsSignedIn));
        session.Other();                                                       // another property: ignored
        await Task.Delay(50, Ct);
        Assert.IsType<HomeVm>(router.CurrentViewModel);

        session.IsSignedIn = false;
        for (var i = 0; i < 200 && router.CurrentViewModel is not LoginVm; i++)
            await Task.Delay(10, Ct);
        Assert.IsType<LoginVm>(router.CurrentViewModel);

        subscription.Dispose();
        await router.GoToAsync("/login", cancellationToken: Ct);
        session.IsSignedIn = true;
        session.IsSignedIn = false;                                            // no longer followed: no extra route change
        await Task.Delay(50, Ct);
        Assert.IsType<LoginVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task RefreshGuardsAsync_with_no_page_does_nothing()
    {
        await using var provider = Build(notFound: false, new Session());
        Assert.True((await provider.GetRequiredService<IRouter>().RefreshGuardsAsync(Ct)).Success);
    }
}
