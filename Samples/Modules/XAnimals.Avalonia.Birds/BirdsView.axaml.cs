using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.Avalonia.Birds;
using XAnimals.Birds;

namespace XAnimals.Avalonia;

public partial class BirdsView : RouteableFor<BirdsViewModel>
{
    public BirdsView()
    {
        InitializeComponent();
    }

    // The module of this platform: its page is registered when it is loaded.
    private void OnLoadModule(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => ViewModel.LoadMoreBirds(services => services.AddAvaloniaMoreBirdsModule());
}
