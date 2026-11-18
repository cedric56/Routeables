using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>Three defects of the fourth audit: a rest after an optional, a view model of a view that could not be built, a redirect that ignores the caller.</summary>
public class RouterAuditFixesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ----------------------------------------------------------------------------------------- RouteParser.Build

    [Fact]
    public void A_rest_after_a_left_out_optional_is_refused_like_any_later_value()
    {
        foreach (var template in new[] { "/cat/{age:int?}/{**rest}", "/cat/{nick?}/{**rest}" })
        {
            var values = new Dictionary<string, object?> { ["rest"] = "a/b" };

            var error = Assert.Throws<ArgumentException>(() => RouteParser.Build(template, values));

            Assert.Contains("cannot leave out", error.Message);
        }
    }

    [Fact]
    public void A_rest_after_a_given_optional_and_a_rest_alone_are_built()
    {
        Assert.Equal("/cat/3/a/b", RouteParser.Build("/cat/{age:int?}/{**rest}", new Dictionary<string, object?> { ["age"] = 3, ["rest"] = "a/b" }));
        Assert.Equal("/cat/3", RouteParser.Build("/cat/{age:int?}/{**rest}", new Dictionary<string, object?> { ["age"] = 3 }));
        Assert.Equal("/files/a/b", RouteParser.Build("/files/{**rest}", new Dictionary<string, object?> { ["rest"] = "a/b" }));
    }

    // ----------------------------------------------------------------------------------------- a view that cannot be built

    public sealed class BuiltVm : IDisposable
    {
        public static int Created;
        public static int Disposed;
        public BuiltVm() => Interlocked.Increment(ref Created);
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    public sealed class BrokenView : IRouteableFor { public object? Context { get; set; } }

    [Fact]
    public async Task A_view_model_whose_view_cannot_be_built_is_disposed()
    {
        BuiltVm.Created = BuiltVm.Disposed = 0;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<BrokenView, BuiltVm>(
            (_, _) => throw new InvalidOperationException("the view cannot be built"), (_, _) => new BuiltVm(),
            ServiceLifetime.Transient, ServiceLifetime.Transient, "/broken");
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        var failed = false;
        try
        {
            var result = await router.GoToAsync("/broken", cancellationToken: Ct);
            failed = !result.Success;
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }

        Assert.True(failed);
        Assert.Equal(1, BuiltVm.Created);
        Assert.Equal(1, BuiltVm.Disposed);
    }

    // ----------------------------------------------------------------------------------------- RefreshGuardsAsync and the caller's token

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class LoginView : IRouteableFor { public object? Context { get; set; } }

    /// <summary>The sign-in page: its arrival lasts until the token it is given is cancelled.</summary>
    public sealed class LoginVm : IRouteable
    {
        public static TaskCompletionSource Arriving { get; set; } = new();

        public async Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            Arriving.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        }
    }

    private sealed class SignedOut : IRouteGuard
    {
        public bool Active { get; set; }

        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
            => Task.FromResult(Active && change.To.ViewModel is HomeVm ? RouteGuardDecision.Redirect("/login") : RouteGuardDecision.Allow);
    }

    [Fact]
    public async Task The_redirection_of_RefreshGuardsAsync_stops_when_the_caller_cancels()
    {
        await StopsWhenTheCallerCancelsAsync(RouterConcurrency.Queue);
        await StopsWhenTheCallerCancelsAsync(RouterConcurrency.CancelCurrent);
    }

    private static async Task StopsWhenTheCallerCancelsAsync(RouterConcurrency concurrency)
    {
        LoginVm.Arriving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var guard = new SignedOut();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.SetConcurrency(concurrency);
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<LoginView, LoginVm>(route: "/login");
        services.AddSingleton<IRouteGuard>(guard);
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        guard.Active = true;

        using var source = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var refresh = router.RefreshGuardsAsync(source.Token);
        await LoginVm.Arriving.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }
}
