using Avalonia.Interactivity;
using Routeables.Avalonia;
using RouteablesApp.ViewModels;

namespace RouteablesApp.Views;

// No attribute: HomeViewModel is [Routeable], and the view called after it (HomeViewModel => HomeView) is found by name.
public partial class HomeView : RouteableFor<HomeViewModel>
{
    public HomeView()
    {
        InitializeComponent();
    }

    private async void OnSayHello(object? sender, RoutedEventArgs e) => await ViewModel.SayHelloAsync();
}
