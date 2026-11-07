using Routeables.Abstraction;
using Xunit;

namespace Routeables.Test;

/// <summary>
/// What every host (Blazor, ASP.NET Core, WPF, Avalonia, MAUI, Uno, WinUI3) must do the same way, written once against the router:
/// the project of a host links this file, registers <c>/a</c> and <c>/b</c> (any page, any view model), and calls <see cref="RunAsync"/>
/// on its container, from the thread that owns its pages. The scenario only uses what the router promises, never what a host draws.
/// </summary>
public static class HostContract
{
    /// <summary>Runs the whole scenario on the default router of <paramref name="provider"/>, leaving it empty.</summary>
    public static async Task RunAsync(IServiceProvider provider, Func<Task>? settle = null)
    {
        settle ??= () => Task.CompletedTask;
        var router = provider.GetRouter(null);
        await router.EraseHistoryAsync();
        Assert.Equal(0, router.History.Count);
        Assert.False(router.CanGoBack);
        Assert.Null(router.CurrentView);

        // Going somewhere makes a view and a view model, and the history has one entry.
        Assert.True((await provider.GoToAsync("/a")).Success);
        await settle();
        Assert.NotNull(router.CurrentView);
        Assert.NotNull(router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        var first = router.CurrentViewModel;

        // A route nobody registered fails without throwing, and changes nothing.
        var unknown = await provider.GoToAsync("/no-such-route-" + Guid.NewGuid().ToString("N"));
        Assert.False(unknown.Success);
        Assert.Same(first, router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);

        // A second page, then Back and Forward go through the same entries.
        Assert.True((await provider.GoToAsync("/b")).Success);
        await settle();
        Assert.Equal(2, router.History.Count);
        Assert.NotSame(first, router.CurrentViewModel);
        Assert.True(router.CanGoBack);
        Assert.False(router.CanGoForward);

        Assert.True((await router.GoBackAsync()).Success);
        await settle();
        Assert.Same(first, router.CurrentViewModel);
        Assert.True(router.CanGoForward);

        Assert.True((await router.ForwardAsync()).Success);
        await settle();
        Assert.False(router.CanGoForward);

        // Going back to the root drops what follows it; Pop leaves a page for good.
        Assert.True((await router.GoBackToRootAsync()).Success);
        await settle();
        Assert.Equal(1, router.History.Count);
        Assert.Same(first, router.CurrentViewModel);
        Assert.True((await provider.GoToAsync("/b")).Success);
        Assert.True((await router.PopAsync()).Success);
        await settle();
        Assert.Equal(1, router.History.Count);
        Assert.False(router.CanGoForward);

        // Routed is raised once for every Routing, whatever the outcome.
        int routing = 0, routed = 0;
        router.Routing += (_, _) => routing++;
        router.Routed += (_, _) => routed++;
        await provider.GoToAsync("/b");
        await provider.GoToAsync("/no-such-route-" + Guid.NewGuid().ToString("N"));
        await router.GoBackAsync();
        Assert.Equal(routing, routed);
        Assert.True(routed >= 2);

        await router.EraseHistoryAsync();
        Assert.Equal(0, router.History.Count);
    }
}
