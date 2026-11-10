using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Routeables.Abstraction;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.History;
using Routeables.Blazor;
using Routeables.Routing;
using System.Security.Claims;

namespace Routeables.Tests.Razor;

/// <summary><c>[Authorize]</c> and <c>[AllowAnonymous]</c> on the view models, asked by <see cref="RouteableAuthorizationGuard"/>.</summary>
public class RouteableAuthorizationGuardTests
{
    public sealed class OpenVm;

    [Authorize]
    public sealed class PrivateVm;

    [Authorize(Roles = "admin")]
    public sealed class AdminVm;

    [Authorize, AllowAnonymous]
    public sealed class SignInVm;

    public sealed class OpenView;

    [Authorize]
    public sealed class PrivateView;

    private sealed class Provider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private sealed class Entry(object viewModel, object view, string? route) : IHistoryEntry
    {
        public object ViewModel { get; } = viewModel;
        public object View { get; } = view;
        public IRouteableArgs Args { get; } = route is null ? RouteableArgs.Create() : RouteableArgs.Create().WithRoute(route);
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal Member(params string[] roles)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann"), .. roles.Select(r => new Claim(ClaimTypes.Role, r))], "test"));

    private static async Task<RouteGuardDecision> AskAsync(ClaimsPrincipal user, object viewModel, string route,
        object? view = null, Action<RouteableAuthorizationOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<AuthenticationStateProvider>(_ => new Provider(user));
        services.AddRouteableAuthorization(configure);
        await using var provider = services.BuildServiceProvider();
        var guard = provider.GetServices<IRouteGuard>().OfType<RouteableAuthorizationGuard>().Single();
        return await guard.CanEnterAsync(
            new RouteableRoutingEventArgs(RouteableKind.Push, null, new Entry(viewModel, view ?? new OpenView(), route)),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_page_without_Authorize_is_open_to_everyone()
        => Assert.True((await AskAsync(Anonymous(), new OpenVm(), "/open")).IsAllowed);

    [Fact]
    public async Task A_signed_out_visitor_is_sent_to_the_sign_in_route()
        => Assert.Equal("/sign-in", (await AskAsync(Anonymous(), new PrivateVm(), "/private", configure: o => o.SignInRoute = "/sign-in")).RedirectTo);

    [Fact]
    public async Task Without_a_sign_in_route_the_route_change_is_cancelled()
    {
        var decision = await AskAsync(Anonymous(), new PrivateVm(), "/private");

        Assert.False(decision.IsAllowed);
        Assert.Null(decision.RedirectTo);
    }

    [Fact]
    public async Task A_signed_in_visitor_enters()
        => Assert.True((await AskAsync(Member(), new PrivateVm(), "/private")).IsAllowed);

    [Fact]
    public async Task A_role_that_is_missing_sends_the_visitor_to_the_forbidden_route()
        => Assert.Equal("/forbidden", (await AskAsync(Member("user"), new AdminVm(), "/admin", configure: o => o.ForbiddenRoute = "/forbidden")).RedirectTo);

    [Fact]
    public async Task The_role_that_is_asked_opens_the_page()
        => Assert.True((await AskAsync(Member("admin"), new AdminVm(), "/admin")).IsAllowed);

    [Fact]
    public async Task AllowAnonymous_opens_a_page_again()
        => Assert.True((await AskAsync(Anonymous(), new SignInVm(), "/sign-in")).IsAllowed);

    [Fact]
    public async Task Authorize_on_the_view_counts_too()
        => Assert.False((await AskAsync(Anonymous(), new OpenVm(), "/open", new PrivateView())).IsAllowed);

    [Fact]
    public async Task A_sign_in_page_that_requires_sign_in_is_cancelled_not_redirected_to_itself()
    {
        var decision = await AskAsync(Anonymous(), new PrivateVm(), "/sign-in", configure: o => o.SignInRoute = "sign-in");

        Assert.False(decision.IsAllowed);
        Assert.Null(decision.RedirectTo);
    }
}
