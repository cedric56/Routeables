using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.Birds;
using XAnimals.Uno.Birds;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(BirdsViewModel), route: BirdsModule.Route)]
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
        viewModel.LoadMoreBirds(services => services.AddUnoMoreBirdsModule());
        LoadButton.Content = "Module loaded";
    }
}
