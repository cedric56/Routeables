using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Routing;

/// <summary>An <see cref="IRouteTypeGuard"/> is asked from the view model type, before the page is built.</summary>
public class RouteTypeGuardTests
{
    public sealed class TgSecretVm { public TgSecretVm() => Built++; public static int Built; }

    private sealed class TypeGuard(Func<Type, RouteGuardDecision> decide, List<string> asked, int order = 0) : IRouteTypeGuard
    {
        public int Order => order;

        public Task<RouteGuardDecision> CanBuildAsync(Type viewModelType, string route, CancellationToken cancellationToken = default)
        {
            asked.Add($"{viewModelType.Name} {route}");
            return Task.FromResult(decide(viewModelType));
        }
    }

    private static ServiceProvider Build(params IRouteTypeGuard[] guards)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/tg-home");
        services.RegisterRoute<SecondViewFor, TgSecretVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/tg-secret");
        services.AddTransient<FakeViewModel>();
        services.AddTransient<TgSecretVm>();
        foreach (var guard in guards)
            services.AddSingleton(guard);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_refused_page_is_never_built()
    {
        var ct = TestContext.Current.CancellationToken;
        TgSecretVm.Built = 0;
        var asked = new List<string>();
        using var provider = Build(new TypeGuard(t => t == typeof(TgSecretVm) ? RouteGuardDecision.Cancel : RouteGuardDecision.Allow, asked));
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/tg-home", cancellationToken: ct);

        var result = await router.GoToAsync("/tg-secret", cancellationToken: ct);

        Assert.Equal(0, TgSecretVm.Built);
        Assert.Contains("TgSecretVm /tg-secret", asked);
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task A_redirected_page_is_never_built_and_the_user_goes_elsewhere()
    {
        var ct = TestContext.Current.CancellationToken;
        TgSecretVm.Built = 0;
        var asked = new List<string>();
        using var provider = Build(new TypeGuard(t => t == typeof(TgSecretVm) ? RouteGuardDecision.Redirect("/tg-home") : RouteGuardDecision.Allow, asked));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/tg-secret", cancellationToken: ct);

        Assert.Equal(0, TgSecretVm.Built);
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);
    }

    [Fact]
    public async Task An_allowed_page_is_built()
    {
        var ct = TestContext.Current.CancellationToken;
        TgSecretVm.Built = 0;
        using var provider = Build(new TypeGuard(_ => RouteGuardDecision.Allow, []));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/tg-secret", cancellationToken: ct);

        Assert.Equal(1, TgSecretVm.Built);
    }

    [Fact]
    public async Task A_type_guard_that_returns_null_fails_the_route_change_and_the_page_is_never_built()
    {
        var ct = TestContext.Current.CancellationToken;
        TgSecretVm.Built = 0;
        using var provider = Build(new TypeGuard(t => t == typeof(TgSecretVm) ? null! : RouteGuardDecision.Allow, []));
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/tg-home", cancellationToken: ct);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoToAsync("/tg-secret", cancellationToken: ct));

        Assert.Contains("returned null", failure.Message);
        Assert.Equal(0, TgSecretVm.Built);
        Assert.IsType<FakeViewModel>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Type_guards_are_asked_by_Order_and_the_first_refusal_stops_the_others()
    {
        var ct = TestContext.Current.CancellationToken;
        TgSecretVm.Built = 0;
        var early = new List<string>();
        var late = new List<string>();
        using var provider = Build(
            new TypeGuard(_ => RouteGuardDecision.Allow, late, order: 5),
            new TypeGuard(t => t == typeof(TgSecretVm) ? RouteGuardDecision.Cancel : RouteGuardDecision.Allow, early, order: -1));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/tg-secret", cancellationToken: ct);

        Assert.Equal(["TgSecretVm /tg-secret"], early);
        Assert.Empty(late);
        Assert.Equal(0, TgSecretVm.Built);
    }

    [Fact]
    public async Task A_query_on_the_address_does_not_hide_the_page_type_from_the_guards()
    {
        var ct = TestContext.Current.CancellationToken;
        TgSecretVm.Built = 0;
        var asked = new List<string>();
        using var provider = Build(new TypeGuard(t => t == typeof(TgSecretVm) ? RouteGuardDecision.Cancel : RouteGuardDecision.Allow, asked));
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/tg-secret?from=mail", cancellationToken: ct);

        Assert.Contains(asked, a => a.StartsWith("TgSecretVm "));
        Assert.Equal(0, TgSecretVm.Built);
    }
}
