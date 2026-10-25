using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Routeables.Abstraction;

namespace Routeables.Uno;

/// <summary>
/// A <see cref="Microsoft.UI.Xaml.Controls.ListView"/> that shows the entries of an <see cref="IMainViewModel"/> and selects the current one.
/// Give it the main view model as <c>DataContext</c> directly or by inheritance:
/// <code>
/// &lt;routeables:RouteableListView /&gt;
/// </code>
/// Choosing an entry goes to it; a locked entry is disabled.
/// </summary>
public partial class RouteableListView : Microsoft.UI.Xaml.Controls.ListView
{
    /// <summary>Creates the list.</summary>
    public RouteableListView()
    {
        SetBinding(ItemsSourceProperty, new Binding { Path = new PropertyPath(nameof(IMainViewModel.Routeables)) });
        SetBinding(SelectedItemProperty, new Binding { Path = new PropertyPath(nameof(IMainViewModel.Routeable)), Mode = BindingMode.TwoWay });
        SelectionMode = ListViewSelectionMode.Single;
        ItemTemplate = (DataTemplate)XamlReader.Load(MenuItemTemplate);
    }

    // Built from text: a template cannot be assembled from code. {Binding} gives the entry itself to the item.
    private const string MenuItemTemplate =
        """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:routeables="using:Routeables.Uno">
            <routeables:RouteableMenuItem Item="{Binding}" />
        </DataTemplate>
        """;
}
