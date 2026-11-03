using System.Windows.Controls;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Wpf;

/// <summary>
/// A <see cref="TabControl"/> written in XAML whose tabs are routes: the tab keeps its header, the router builds its page.
/// <code>
/// &lt;TabControl x:Name="Tabs"&gt;
///   &lt;TabItem Header="Cats" routeables:RouteableItem.Route="cats" /&gt;   &lt;!-- the route "/cats" --&gt;
///   &lt;TabItem Header="Dogs" routeables:RouteableItem.Route="dogs" /&gt;
/// &lt;/TabControl&gt;
/// </code>
/// then <c>Tabs.AttachRouteables(provider);</c> once the control is built.
/// </summary>
public static class RouteableTabs
{
    /// <summary>
    /// Shows the page of the selected tab in a <see cref="RouteableRegion"/>, routes when the user chooses another tab, and selects the
    /// tab of a route that something else reached (Back, a link, code).
    /// </summary>
    /// <remarks>
    /// The route of a tab is its <c>RouteableItem.Route</c>. A tab without one or with a <c>Content</c> of its own is left alone. The tabs are read
    /// when this is called. There is one region, moved to the selected tab: the pages are never shown twice.
    /// </remarks>
    /// <param name="tabs">The control.</param>
    /// <param name="provider">The container of the host.</param>
    /// <param name="region">The region (<c>services.AddRegion(name)</c>) whose history the tabs have; the default router when <see langword="null"/>.</param>
    /// <returns>Stops following the control and the router when disposed.</returns>
    public static IDisposable AttachRouteables(this TabControl tabs, IServiceProvider provider, string? region = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(provider);

        var router = provider.GetRouter(region);
        var host = new RouteableRegion { Region = region, Services = provider };
        var owned = tabs.Items.OfType<TabItem>().Where(tab => RouteableItem.GetRoute(tab) is not null && tab.Content is null).ToList();

        void MoveHost()
        {
            foreach (var tab in owned.Where(tab => ReferenceEquals(tab.Content, host) && !ReferenceEquals(tab, tabs.SelectedItem)))
                tab.Content = null;

            if (tabs.SelectedItem is TabItem selected && owned.Contains(selected) && selected.Content is null)
                selected.Content = host;
        }

        void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // The selections of the controls inside the pages bubble up to here.
            if (!ReferenceEquals(e.OriginalSource, tabs))
                return;

            MoveHost();
            if (tabs.SelectedItem is TabItem selected && owned.Contains(selected) && RouteableItem.GetRoute(selected) is { } route)
                _ = RouteTarget.GoAsync(provider, router, route, typeof(RouteableTabs));
        }

        // The router holds its gate while it signals: the tab is selected after, and does not route again (the route is the current one).
        void OnChanged(object? sender, EventArgs e) => tabs.Dispatcher.InvokeAsync(() =>
        {
            var tab = owned.FirstOrDefault(tab => RouteTarget.IsCurrent(router, RouteableItem.GetRoute(tab)));
            if (tab is not null && !ReferenceEquals(tab, tabs.SelectedItem))
                tabs.SelectedItem = tab;
        });

        tabs.SelectionChanged += OnSelectionChanged;
        router.History.Changed += OnChanged;

        // The tab that is selected now shows its route.
        tabs.Dispatcher.InvokeAsync(() =>
        {
            MoveHost();
            if (tabs.SelectedItem is TabItem selected && owned.Contains(selected) && RouteableItem.GetRoute(selected) is { } route)
                _ = RouteTarget.GoAsync(provider, router, route, typeof(RouteableTabs));
        });

        return new Subscription(() =>
        {
            tabs.SelectionChanged -= OnSelectionChanged;
            router.History.Changed -= OnChanged;
            foreach (var tab in owned.Where(tab => ReferenceEquals(tab.Content, host)))
                tab.Content = null;
        });
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
