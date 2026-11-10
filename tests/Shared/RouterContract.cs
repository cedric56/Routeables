using Routeables.Abstraction;
using Routeables.Abstraction.Routing;

namespace Routeables.Test;

/// <summary>
/// What every host (Blazor, ASP.NET Core, WPF, Avalonia, MAUI, Uno, WinUI3) must do the same way, written once against the router:
/// the project of a host links this file, registers <c>/a</c> and <c>/b</c> (any page, any view model), and calls <see cref="RunAsync"/>
/// on its container, from the thread that owns its pages. The scenario only uses what the router promises, never what a host draws.
/// </summary>
public static class RouterContract
{
    /// <summary>Runs the whole scenario on the default router of <paramref name="provider"/>, leaving it empty.</summary>
    public static async Task RunAsync(IServiceProvider provider, Func<Task>? settle = null)
    {
        settle ??= () => Task.CompletedTask;
        var router = provider.GetRouter(null);
        await router.EraseHistoryAsync();
        Check.Equal(0, router.History.Count);
        Check.False(router.CanGoBack);
        Check.Null(router.CurrentView);

        // Going somewhere makes a view and a view model, and the history has one entry.
        Check.True((await provider.GoToAsync("/a")).Success);
        await settle();
        Check.NotNull(router.CurrentView);
        Check.NotNull(router.CurrentViewModel);
        Check.Equal(1, router.History.Count);
        var first = router.CurrentViewModel;

        // A route nobody registered fails without throwing, and changes nothing.
        var unknown = await provider.GoToAsync("/no-such-route-" + Guid.NewGuid().ToString("N"));
        Check.False(unknown.Success);
        Check.Same(first, router.CurrentViewModel);
        Check.Equal(1, router.History.Count);

        // A second page, then Back and Forward go through the same entries.
        Check.True((await provider.GoToAsync("/b")).Success);
        await settle();
        Check.Equal(2, router.History.Count);
        Check.NotSame(first, router.CurrentViewModel);
        Check.True(router.CanGoBack);
        Check.False(router.CanGoForward);

        Check.True((await router.GoBackAsync()).Success);
        await settle();
        Check.Same(first, router.CurrentViewModel);
        Check.True(router.CanGoForward);

        Check.True((await router.ForwardAsync()).Success);
        await settle();
        Check.False(router.CanGoForward);

        // Going back to the root drops what follows it; Pop leaves a page for good.
        Check.True((await router.GoBackToRootAsync()).Success);
        await settle();
        Check.Equal(1, router.History.Count);
        Check.Same(first, router.CurrentViewModel);
        Check.True((await provider.GoToAsync("/b")).Success);
        Check.True((await router.PopAsync()).Success);
        await settle();
        Check.Equal(1, router.History.Count);
        Check.False(router.CanGoForward);

        // Routed is raised once for every Routing, whatever the outcome.
        int routing = 0, routed = 0;
        router.Routing += (_, _) => routing++;
        router.Routed += (_, _) => routed++;
        await provider.GoToAsync("/b");
        await provider.GoToAsync("/no-such-route-" + Guid.NewGuid().ToString("N"));
        await router.GoBackAsync();
        Check.Equal(routing, routed);
        Check.True(routed >= 2);

        await router.EraseHistoryAsync();
        Check.Equal(0, router.History.Count);
    }

    /// <summary>The checks, written here so the file compiles in every test project whatever its framework (xUnit, MSTest).</summary>
    private static class Check
    {
        public static void True(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null)
        { if (!condition) throw new InvalidOperationException($"Contract broken: {what} should be true."); }

        public static void False(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null)
        { if (condition) throw new InvalidOperationException($"Contract broken: {what} should be false."); }

        public static void Equal(int expected, int actual, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(actual))] string? what = null)
        { if (expected != actual) throw new InvalidOperationException($"Contract broken: {what} is {actual}, expected {expected}."); }

        public static void Null(object? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? what = null)
        { if (value is not null) throw new InvalidOperationException($"Contract broken: {what} should be null."); }

        public static void NotNull(object? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? what = null)
        { if (value is null) throw new InvalidOperationException($"Contract broken: {what} should not be null."); }

        public static void Same(object? expected, object? actual, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(actual))] string? what = null)
        { if (!ReferenceEquals(expected, actual)) throw new InvalidOperationException($"Contract broken: {what} is not the expected instance."); }

        public static void NotSame(object? other, object? actual, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(actual))] string? what = null)
        { if (ReferenceEquals(other, actual)) throw new InvalidOperationException($"Contract broken: {what} should be another instance."); }
    }
}
