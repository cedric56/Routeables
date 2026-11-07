using Routeables.WinUI3;
using XAnimals.Birds;
using XAnimals.WinUI3.Birds;

namespace XAnimals.WinUI3.Views;

public sealed partial class BirdsView : RouteableFor
{
    public BirdsView()
    {
        InitializeComponent();
    }

    // The module of this platform: its page is registered when it is loaded.
    private void OnLoadModule(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not BirdsViewModel viewModel) return;
        viewModel.LoadMoreBirds(services => services.AddWinUI3MoreBirdsModule());
        LoadButton.Content = "Module loaded";
    }
}
