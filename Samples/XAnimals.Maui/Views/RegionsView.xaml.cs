using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(RegionsViewModel), route: "regions")]
public partial class RegionsView : RouteableFor
{
    public RegionsView()
    {
        InitializeComponent();
    }

    private bool _collapsed;

    /// <summary>
    /// Reduces the left panel to its button, or brings it back. The column of the content is a star: it takes the place that
    /// is freed, and the panel keeps its page (it is hidden, not removed).
    /// </summary>
    private void OnCollapseClicked(object? sender, EventArgs e)
    {
        _collapsed = !_collapsed;
        ShellGrid.ColumnDefinitions[0].Width = _collapsed ? new GridLength(56) : new GridLength(190);
        LeftScroll.IsVisible = CreditsButton.IsVisible = !_collapsed;
        CollapseButton.Text = _collapsed ? "»" : "«";
        CollapseButton.HorizontalOptions = _collapsed ? LayoutOptions.Center : LayoutOptions.End;
        SemanticProperties.SetDescription(CollapseButton, _collapsed ? "Show the panel" : "Reduce the panel");
    }
}
