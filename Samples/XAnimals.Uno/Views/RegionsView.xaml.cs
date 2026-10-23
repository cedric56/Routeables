using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;
using Microsoft.UI.Xaml;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(RegionsViewModel), route: "regions")]
public sealed partial class RegionsView : RouteableFor
{
    public RegionsView()
    {
        InitializeComponent();
    }

    private bool _collapsed;

    /// <summary>
    /// Reduces the left panel to its button, or brings it back. The column of the content is a star: it takes the place that is
    /// freed, and the panel keeps its page (it is hidden, not removed).
    /// </summary>
    private void OnCollapseClick(object sender, RoutedEventArgs e)
    {
        _collapsed = !_collapsed;
        Layout.ColumnDefinitions[0].Width = new GridLength(_collapsed ? 56 : 230);
        LeftScroll.Visibility = CreditsButton.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseButton.Content = _collapsed ? "»" : "«";
        CollapseButton.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        var label = _collapsed ? "Show the panel" : "Reduce the panel";
        ToolTipService.SetToolTip(CollapseButton, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CollapseButton, label);
    }
}
