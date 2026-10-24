using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>A page registered as a singleton is shared: leaving the history must not dispose it while the container keeps handing it out.</summary>
public class RouterSingletonLifetimeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Page : IDisposable
    {
        public int Disposed { get; private set; }
        public void Dispose() => Disposed++;
    }

    [Fact]
    public async Task A_singleton_page_cut_off_from_the_history_is_not_disposed_and_can_be_shown_again()
    {
        var services = new ServiceCollection();
        services.RegisterRoute("/home", (_, _) => new Page(), ServiceLifetime.Transient);
        services.RegisterRoute("/shared", (_, _) => new Page(), ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();
        var router = new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(provider), provider);

        await router.GoToAsync("/home", template: "/home", cancellationToken: Ct);
        await router.GoToAsync("/shared", template: "/shared", cancellationToken: Ct);
        var shared = (Page)router.CurrentView!;
        await router.GoBackToRootAsync(Ct);

        await router.GoToAsync("/shared", template: "/shared", cancellationToken: Ct);

        Assert.Same(shared, router.CurrentView);
        Assert.Equal(0, shared.Disposed);
    }

    [Fact]
    public async Task A_scoped_page_cut_off_from_the_history_is_disposed_with_its_scope_not_before()
    {
        var services = new ServiceCollection();
        services.RegisterRoute("/home", (_, _) => new Page(), ServiceLifetime.Transient);
        services.RegisterRoute("/scoped", (_, _) => new Page(), ServiceLifetime.Scoped);
        using var root = services.BuildServiceProvider();
        var scope = root.CreateScope();
        var router = new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(scope.ServiceProvider), scope.ServiceProvider);

        await router.GoToAsync("/home", template: "/home", cancellationToken: Ct);
        await router.GoToAsync("/scoped", template: "/scoped", cancellationToken: Ct);
        var page = (Page)router.CurrentView!;
        await router.GoBackToRootAsync(Ct);

        Assert.Equal(0, page.Disposed);

        scope.Dispose();

        Assert.Equal(1, page.Disposed);
    }
}
