using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Locking;

namespace Routeables.Tests.Core;

/// <summary>A scope is a Blazor circuit: once it is disposed, nothing it created may stay alive.</summary>
public class ScopeRetentionTests
{
    private sealed class Page : IDisposable
    {
        public Page() => Pages.Add(new WeakReference(this));
        public void Dispose() { }
    }

    private static readonly List<WeakReference> Pages = [];
    private static readonly List<WeakReference> Mains = [];

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.RegisterRouteableArgsFactory(() => new RouteableArgs());
        services.RegisterRoute("/page", (_, _) => new Page(), ServiceLifetime.Scoped);
        services.AddScoped<IMainRouteables>(sp =>
        {
            var main = TestNavigableViewModel.Create(sp.GetRequiredService<IRouter>(), [], initialize: false);
            Mains.Add(new WeakReference(main));
            return main;
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // Own method: no local of the caller keeps the scope's objects alive.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task UseAndDisposeScopes(ServiceProvider root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await using var scope = root.CreateAsyncScope();
            var main = scope.ServiceProvider.GetRequiredService<IMainRouteables>();
            var router = scope.ServiceProvider.GetRequiredService<IRouter>();
            for (var p = 0; p < 3; p++)
                await router.GoToAsync("/page", template: "/page");
            main.Lock();
            main.Unlock();
        }
    }

    [Fact]
    public async Task Disposed_scopes_leave_no_main_view_model_and_no_page_alive()
    {
        Pages.Clear();
        Mains.Clear();
        using var root = Build();

        await UseAndDisposeScopes(root, 50);

        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        var mains = Mains.Count(w => w.IsAlive);
        var pages = Pages.Count(w => w.IsAlive);

        Assert.Equal(50, Mains.Count);
        Assert.True(mains <= 1, $"{mains} main view models of disposed scopes are still alive");
        Assert.True(pages <= 1, $"{pages} pages of disposed scopes are still alive");
    }

    private static readonly List<WeakReference> RegionPages = [];

    private sealed class RegionPage
    {
        public RegionPage() => RegionPages.Add(new WeakReference(this));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task UseRegionScopes(ServiceProvider root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await using var scope = root.CreateAsyncScope();
            var main = scope.ServiceProvider.GetRouter("main");
            var side = scope.ServiceProvider.GetRouter("side");
            for (var p = 0; p < 3; p++)
            {
                await main.GoToAsync("/region", template: "/region");
                await side.GoToAsync("/region", template: "/region");
            }
        }
    }

    [Fact]
    public async Task Disposed_scopes_leave_no_region_page_alive()
    {
        RegionPages.Clear();
        var services = new ServiceCollection();
        services.RegisterRouteableArgsFactory(() => new RouteableArgs());
        services.RegisterRoute("/region", (_, _) => new RegionPage(), ServiceLifetime.Transient);
        services.AddRegion("main");
        services.AddRegion("side");
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await UseRegionScopes(root, 30);

        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        var alive = RegionPages.Count(w => w.IsAlive);

        Assert.True(RegionPages.Count >= 30 * 6, "pages were created");
        Assert.True(alive <= 2, $"{alive} region pages of disposed scopes are still alive");
    }
}
