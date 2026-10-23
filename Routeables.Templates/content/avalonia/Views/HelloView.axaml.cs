using Avalonia.Interactivity;
using Routeables.Avalonia;
using RouteablesApp.ViewModels;

namespace RouteablesApp.Views;

public partial class HelloView : RouteableFor<HelloViewModel>
{
    public HelloView()
    {
        InitializeComponent();
    }

    private async void OnBack(object? sender, RoutedEventArgs e) => await ViewModel.BackAsync();
}
