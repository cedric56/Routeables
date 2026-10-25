using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Maui;

/// <summary>
/// A <see cref="Shell"/> written in XAML (<c>FlyoutItem</c>, <c>Tab</c>, <c>ShellContent</c> with their <c>Route</c>) whose pages are the routes of
/// Routeables. Shell keeps the structure, the flyout and the tabs; the router builds each page.
/// <code>
/// &lt;Shell&gt;
///   &lt;FlyoutItem Title="Animals" Route="animals"&gt;
///     &lt;Tab Title="Domestic" Route="domestic"&gt;          &lt;!-- AddRegion("domestic"): a history of its own --&gt;
///       &lt;ShellContent Title="Cats" Route="cats" /&gt;     &lt;!-- the route "/cats" --&gt;
///       &lt;ShellContent Title="Dogs" Route="dogs" /&gt;
///     &lt;/Tab&gt;
///   &lt;/FlyoutItem&gt;
///   &lt;ShellContent Title="About" Route="about" /&gt;
/// &lt;/Shell&gt;
/// </code>
/// </summary>
public static class ShellItems
{
    /// <summary>
    /// Gives each <c>ShellContent</c> that has no content of its own a <see cref="RouteableRegion"/>, and routes it when it is selected.
    /// </summary>
    /// <remarks>
    /// The route of a <c>ShellContent</c> is <c>"/" + Route</c>: register it as any route of Routeables. A <c>Tab</c> whose <c>Route</c> is
    /// the name of a region (<c>services.AddRegion("domestic")</c>) has the history of that region; the others share the default router.
    /// A <c>ShellContent</c> without a <c>Route</c>, or that has a <c>Content</c> or a <c>ContentTemplate</c>, is left to Shell. The items are
    /// read when this is called: call it once the shell is built and before it is shown.
    /// <para>
    /// Do not combine it with <see cref="ShellRoutes.UseShellRoutes"/>: the address Shell gives to an item (<c>//about</c>) is also the route
    /// <c>/about</c>, so the router would take the selection and Shell would cancel it.
    /// </para>
    /// </remarks>
    /// <param name="shell">The shell of the application.</param>
    /// <param name="provider">The container of the host.</param>
    /// <returns>Stops following the selection when disposed.</returns>
    public static IDisposable AttachRouteables(this Shell shell, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(provider);

        var routed = new Dictionary<ShellContent, (string Route, string? Region)>();
        foreach (var item in shell.Items)
        {
            foreach (var section in item.Items)
            {
                var region = RegionOf(provider, section.Route);
                foreach (var content in section.Items)
                {
                    if (content.Content is not null || content.ContentTemplate is not null || !IsDeclared(content.Route))
                        continue;

                    routed[content] = ("/" + content.Route.Trim('/'), region);
                    content.ContentTemplate = new DataTemplate(() => NewPage(provider, region));
                }
            }
        }

        void OnNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            if (shell.CurrentItem?.CurrentItem?.CurrentItem is { } current && routed.TryGetValue(current, out var target))
                _ = Show(provider, target.Route, target.Region);
        }

        shell.Navigated += OnNavigated;
        return new Subscription(() => shell.Navigated -= OnNavigated);
    }

    // Shell keeps the pages it has shown, and the regions of those pages are all bound to the same router: the last one built would take
    // every view. The region exists only while its page is displayed.
    private static ContentPage NewPage(IServiceProvider provider, string? region)
    {
        var page = new ContentPage();
        page.Appearing += (_, _) => page.Content = new RouteableRegion { Region = region, Services = provider };
        page.Disappearing += (_, _) => page.Content = null;
        return page;
    }

    // A route that the XAML wrote, not the one Shell invents for an item that has none ("IMPL_" followed by its title).
    private static bool IsDeclared(string? route)
        => !string.IsNullOrWhiteSpace(route) && !route.StartsWith("IMPL_", StringComparison.Ordinal);

    // The name of a tab is the name of a region when one is registered under it; the default router otherwise.
    private static string? RegionOf(IServiceProvider provider, string? route)
        => IsDeclared(route) && provider.GetKeyedService<IRouter>(route) is not null ? route : null;

    private static async Task Show(IServiceProvider provider, string route, string? region)
    {
        try
        {
            var router = provider.GetRouter(region);
            if (string.Equals(Trim(router.History.Current?.Args.Route), Trim(route), StringComparison.OrdinalIgnoreCase))
                return;

            await router.GoToAsync(route);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, ex, typeof(ShellItems), route);
        }

        static string Trim(string? value) => (value ?? string.Empty).Trim('/');
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
