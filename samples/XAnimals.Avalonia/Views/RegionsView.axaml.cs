using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;
using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;

namespace XAnimals.Avalonia;

public partial class RegionsView : RouteableFor<RegionsViewModel>
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
    private void OnCollapseClick(object? sender, RoutedEventArgs e)
    {
        _collapsed = !_collapsed;
        Layout.ColumnDefinitions[0].Width = new GridLength(_collapsed ? 56 : 230);
        LeftScroll.IsVisible = CreditsButton.IsVisible = !_collapsed;
        CollapseButton.Content = _collapsed ? "»" : "«";
        CollapseButton.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        var label = _collapsed ? "Show the panel" : "Reduce the panel";
        ToolTip.SetTip(CollapseButton, label);
        AutomationProperties.SetName(CollapseButton, label);
    }
}
