using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Routing;
using Routeables.Regions;

namespace Routeables.Avalonia;

/// <summary>
/// Connects the page controls of Avalonia 12 (<see cref="NavigationPage"/>, <see cref="DrawerPage"/>, <see cref="TabbedPage"/>) to a router.
/// <code>
/// var navigation = new NavigationPage();
/// navigation.AttachRouteables(provider);      // every route change pushes, pops or replaces a page
/// </code>
/// </summary>
public static class RouteablePages
{
    /// <summary>
    /// Makes the pile of pages of a <see cref="NavigationPage"/> follow the history of the router: a route change pushes a page, Back pops it,
    /// Forward pushes it again. The back button and the swipe of the control go through the router, which can refuse (<c>CanLeaveAsync</c>, guards):
    /// the page then stays.
    /// </summary>
    /// <remarks>
    /// The view of each route is the content of a <see cref="ContentPage"/> the bridge creates (a view that is a <see cref="Page"/> is used as it is).
    /// The history is the truth: a page pushed by other means is replaced. A view must not be shown twice in the pile (a singleton view reached
    /// twice in a row); one page per entry.
    /// </remarks>
    /// <param name="navigation">The control.</param>
    /// <param name="provider">The container of the host.</param>
    /// <param name="region">The region (<c>services.AddRegion(name)</c>) whose history the control shows; the default router when <see langword="null"/>.</param>
    /// <param name="header">The header (the title of the navigation bar) of the page of an entry; by default the name of its view model without <c>ViewModel</c>, in words.</param>
    /// <returns>Stops following the control and the router when disposed.</returns>
    public static IDisposable AttachRouteables(this NavigationPage navigation, IServiceProvider provider, string? region = null,
        Func<IHistoryEntry, object?>? header = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(provider);
        var bridge = new NavigationPageBridge(navigation, provider, region, header);
        bridge.Start();
        return new RegionSubscription(bridge.Dispose);
    }

    /// <summary>
    /// Makes a <see cref="DrawerPage"/> a shell for the router: its <see cref="DrawerPage.Content"/> is a <see cref="NavigationPage"/> connected like
    /// <see cref="AttachRouteables(NavigationPage, IServiceProvider, string?, Func{IHistoryEntry, object?}?)"/> (the one that is already there, or a new one),
    /// and the drawer closes when a route change succeeded. Put the menu (<see cref="RouteableMenu"/>) in <see cref="DrawerPage.Drawer"/>.
    /// </summary>
    /// <param name="drawer">The control.</param>
    /// <param name="provider">The container of the host.</param>
    /// <param name="region">The region whose history the control shows; the default router when <see langword="null"/>.</param>
    /// <param name="header">See the overload for <see cref="NavigationPage"/>.</param>
    /// <param name="closeOnRoute">Closes the drawer after a route change that succeeded.</param>
    /// <returns>Stops following the control and the router when disposed.</returns>
    public static IDisposable AttachRouteables(this DrawerPage drawer, IServiceProvider provider, string? region = null,
        Func<IHistoryEntry, object?>? header = null, bool closeOnRoute = true)
    {
        ArgumentNullException.ThrowIfNull(drawer);
        ArgumentNullException.ThrowIfNull(provider);

        var created = false;
        if (drawer.Content is not NavigationPage navigation)
        {
            navigation = new NavigationPage();
            drawer.Content = navigation;
            created = true;
        }

        var pages = navigation.AttachRouteables(provider, region, header);
        var router = provider.GetRouter(region);

        void OnRouted(object? sender, RouteableRoutedEventArgs e)
        {
            if (closeOnRoute && e.Succeeded)
                Dispatcher.UIThread.Post(() => drawer.IsOpen = false);
        }

        router.Routed += OnRouted;
        return new RegionSubscription(() =>
        {
            router.Routed -= OnRouted;
            pages.Dispose();
            if (created && ReferenceEquals(drawer.Content, navigation))
                drawer.Content = null;
        });
    }

    /// <summary>
    /// Shows the page of the selected tab of a <see cref="TabbedPage"/> in a <see cref="RouteableRegion"/>, routes when the user chooses another tab,
    /// and selects the tab of a route that something else reached (Back, a link, code). The tabs are <see cref="ContentPage"/>s declared like the
    /// tabs of a <see cref="TabControl"/>: <c>&lt;ContentPage Header="Cats" routeables:RouteableItem.Route="cats" /&gt;</c>.
    /// </summary>
    /// <remarks>A tab without a route or with a <c>Content</c> of its own is left alone. The tabs are read when this is called.</remarks>
    /// <param name="tabs">The control.</param>
    /// <param name="provider">The container of the host.</param>
    /// <param name="region">The region whose history the tabs have; the default router when <see langword="null"/>.</param>
    /// <returns>Stops following the control and the router when disposed.</returns>
    public static IDisposable AttachRouteables(this TabbedPage tabs, IServiceProvider provider, string? region = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(provider);

        var pages = (tabs.Pages ?? []).OfType<ContentPage>().ToList();
        var host = new RouteableRegion { Region = region, Services = provider };
        var follower = new RegionTabs<ContentPage>(provider, region, host, pages,
            RouteableItem.GetRoute, page => page.Content, (page, content) => page.Content = content,
            () => tabs.SelectedPage as ContentPage, page => { var index = tabs.IndexOfPage(page); if (index >= 0) tabs.SelectedIndex = index; },
            action => Dispatcher.UIThread.Post(action), typeof(RouteablePages),
            RouteableItem.GetRegion, name => new RouteableRegion { Region = name, Services = provider });

        void OnSelectionChanged(object? sender, PageSelectionChangedEventArgs e)
        {
            // The selections of the controls inside the pages bubble up to here.
            if (ReferenceEquals(e.Source, tabs))
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

    // The index of a page among the pages of the control (the ones the user sees), -1 when it is not one of them.
    private static int IndexOfPage(this TabbedPage tabs, Page page)
    {
        var index = 0;
        foreach (var candidate in tabs.Pages ?? [])
        {
            if (ReferenceEquals(candidate, page)) return index;
            index++;
        }
        return -1;
    }
}
