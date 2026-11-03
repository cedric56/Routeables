using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Uno;

/// <summary>
/// A <see cref="NavigationView"/> written in XAML whose items are routes: the item keeps its title and icon, the router builds its page.
/// <code>
/// &lt;NavigationView x:Name="Nav"&gt;
///   &lt;NavigationViewItem Content="Cats" routeables:RouteableItem.Route="cats" /&gt;   &lt;!-- the route "/cats" --&gt;
///   &lt;NavigationViewItem Content="Dogs" routeables:RouteableItem.Route="dogs" /&gt;
/// &lt;/NavigationView&gt;
/// </code>
/// then <c>Nav.AttachRouteables(provider);</c> once the control is built.
/// </summary>
public static class RouteableNavigation
{
    /// <summary>
    /// Shows the page of the selected item in a <see cref="RouteableRegion"/>, routes when the user chooses another item, and selects the
    /// item of a route that something else reached (Back, a link, code).
    /// </summary>
    /// <remarks>
    /// The route of an item is its <c>RouteableItem.Route</c>; the items of <c>MenuItems</c> and <c>FooterMenuItems</c>, sub-items included, are read when
    /// this is called. An item without one (a header, a separator, the settings) is left alone. The <c>Content</c> of the view is
    /// replaced by the region. The back button of the view is not handled here.
    /// </remarks>
    /// <param name="view">The control.</param>
    /// <param name="provider">The container of the host.</param>
    /// <param name="region">The region (<c>services.AddRegion(name)</c>) whose history the items have; the default router when <see langword="null"/>.</param>
    /// <returns>Stops following the control and the router when disposed.</returns>
    public static IDisposable AttachRouteables(this NavigationView view, IServiceProvider provider, string? region = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(provider);

        var router = provider.GetRouter(region);
        var items = Items(view.MenuItems).Concat(Items(view.FooterMenuItems)).ToList();
        view.Content = new RouteableRegion { Region = region, Services = provider };

        void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is NavigationViewItem item && items.Contains(item) && RouteableItem.GetRoute(item) is { } route)
                _ = RouteTarget.GoAsync(provider, router, route, typeof(RouteableNavigation));
        }

        // The router holds its gate while it signals: the item is selected after, and does not route again (the route is the current one).
        void OnChanged(object? sender, EventArgs e) => view.DispatcherQueue.TryEnqueue(() =>
        {
            var item = items.FirstOrDefault(item => RouteTarget.IsCurrent(router, RouteableItem.GetRoute(item)));
            if (item is not null && !ReferenceEquals(item, view.SelectedItem))
                view.SelectedItem = item;
        });

        view.SelectionChanged += OnSelectionChanged;
        router.History.Changed += OnChanged;

        // The item that is selected now shows its route; without one, the first.
        view.DispatcherQueue.TryEnqueue(() =>
        {
            if (view.SelectedItem is null && items.Count > 0)
                view.SelectedItem = items[0];
            else if (view.SelectedItem is NavigationViewItem item && items.Contains(item) && RouteableItem.GetRoute(item) is { } route)
                _ = RouteTarget.GoAsync(provider, router, route, typeof(RouteableNavigation));
        });

        return new Subscription(() =>
        {
            view.SelectionChanged -= OnSelectionChanged;
            router.History.Changed -= OnChanged;
        });
    }

    // The items that name a route, at any depth.
    private static IEnumerable<NavigationViewItem> Items(IList<object> source)
    {
        foreach (var entry in source.OfType<NavigationViewItem>())
        {
            if (RouteableItem.GetRoute(entry) is not null)
                yield return entry;

            foreach (var child in Items(entry.MenuItems))
                yield return child;
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
