using Routeables.Abstraction;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The Tabs page: a tab control (WPF, Avalonia) or a navigation view (Uno) declared in the XAML of the view, whose items are routes
/// (<c>Tag="cats"</c>). The view calls <c>AttachRouteables</c> on it, and the router builds the page of each item. The tabs route the
/// region <see cref="Region"/>, not the default router: they would otherwise replace the page that holds them.
/// </summary>
[Routeable("tabs")]
public class TabsViewModel : ViewModelBase
{
    /// <summary>The router the tabs route: a region with its own history.</summary>
    public const string Region = "tabs-demo";
}
