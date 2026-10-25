using System.Windows;
using Routeables.Wpf;
using XAnimals.Birds;
using XAnimals.Wpf.Birds;

namespace XAnimals.Wpf.Views;

public partial class BirdsView : RouteableFor
{
    public BirdsView()
    {
        InitializeComponent();
    }

    // The module of this platform: its page is registered when it is loaded.
    private void OnLoadModule(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BirdsViewModel viewModel) return;
        viewModel.LoadMoreBirds(services => services.AddWpfMoreBirdsModule());
        LoadButton.Content = "Module loaded";
    }
}
