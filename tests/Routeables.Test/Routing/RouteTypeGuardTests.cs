using Routeables.Abstraction.Bases;
using Routeables.Abstraction;
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

    private sealed class TypeGuard(Func<Type, RouteGuardDecision> decide, List<string> asked) : IRouteTypeGuard
    {
        public Task<RouteGuardDecision> CanBuildAsync(Type viewModelType, string route, CancellationToken cancellationToken = default)
        {
            asked.Add($"{viewModelType.Name} {route}");
            return Task.FromResult(decide(viewModelType));
        }
    }

    private static ServiceProvider Build(IRouteTypeGuard guard)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<TgHomeView, TgHomeVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/tg-home");
        services.RegisterRoute<TgSecretView, TgSecretVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/tg-secret");
        services.AddTransient<TgHomeVm>();
        services.AddTransient<TgSecretVm>();
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
        Assert.IsType<TgHomeVm>(router.CurrentViewModel);
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
        Assert.IsType<TgHomeVm>(router.CurrentViewModel);
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
}
