using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Maui;

/// <summary>
/// Lets <c>Shell.Current.GoToAsync("//orders/42")</c> reach the router: an address that matches a route of Routeables is routed by the
/// default router, an address Shell knows (<c>Routing.RegisterRoute</c>) is left to Shell. Opt-in; nothing changes without it.
/// </summary>
public static class ShellRoutes
{
    /// <summary>
    /// Follows <see cref="Shell.Navigating"/>: for a navigation that Shell was asked to make (not Back, and not a pop of its stack), the
    /// router is asked for the address first. When it goes there, or when a guard refuses, Shell's own navigation is cancelled;
    /// when no route matches, Shell goes on as it would have.
    /// </summary>
    /// <param name="shell">The shell of the application.</param>
    /// <param name="provider">The container of the host.</param>
    /// <returns>Stops following when disposed.</returns>
    public static IDisposable UseShellRoutes(this Shell shell, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(provider);

        void OnNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            if (!e.CanCancel || !IsRequest(e.Source) || RouteOf(e) is not { } route)
                return;

            // The decision is asynchronous (the router runs guards and the lifecycle): Shell waits for it.
            var deferral = e.GetDeferral();
            _ = Route(provider, e, route, deferral);
        }

        shell.Navigating += OnNavigating;
        return new Subscription(() => shell.Navigating -= OnNavigating);
    }

    // A pop, a pop to root or a removal belongs to Shell's stack; the other sources are a request for an address.
    private static bool IsRequest(ShellNavigationSource source)
        => source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot or ShellNavigationSource.Remove or ShellNavigationSource.Insert);

    // "//orders/42?x=1", "/orders/42" and "orders/42" are the same address; nothing after the slashes is nothing to route.
    private static string? RouteOf(ShellNavigatingEventArgs e)
    {
        var text = e.Target?.Location.OriginalString?.Trim();
        var path = text?.Trim('/');
        return string.IsNullOrEmpty(path) ? null : "/" + path;
    }

    private static async Task Route(IServiceProvider provider, ShellNavigatingEventArgs e, string route, ShellNavigatingDeferral deferral)
    {
        try
        {
            var router = provider.GetRequiredService<IRouter>();
            var result = await router.GoToAsync(route);

            // Done, or refused by a guard: the router has answered. Only an address it does not know is left to Shell.
            if (result.Success || result.Cancelled)
                e.Cancel();
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, ex, typeof(ShellRoutes));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
