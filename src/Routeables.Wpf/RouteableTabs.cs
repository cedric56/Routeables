using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Regions;
using System.Windows.Controls;

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

        var tabHost = new RouteableRegion { Region = region, Services = provider };
        var follower = new RegionTabs<TabItem>(provider, region, tabHost, tabs.Items.OfType<TabItem>(),
            RouteableItem.GetRoute, tab => tab.Content, (tab, content) => tab.Content = content,
            () => tabs.SelectedItem as TabItem, tab => tabs.SelectedItem = tab,
            action => tabs.Dispatcher.InvokeAsync(action), typeof(RouteableTabs));

        void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // The selections of the controls inside the pages bubble up to here.
            if (ReferenceEquals(e.OriginalSource, tabs))
                follower.OnSelectionChanged();
        }

        tabs.SelectionChanged += OnSelectionChanged;
        follower.Start();

        return new RegionSubscription(() =>
        {
            tabs.SelectionChanged -= OnSelectionChanged;
            follower.Dispose();
        });
    }
}
