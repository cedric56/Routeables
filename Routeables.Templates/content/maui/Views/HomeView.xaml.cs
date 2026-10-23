using Routeables.Maui;
using RouteablesApp.ViewModels;

namespace RouteablesApp.Views;

// No attribute: HomeViewModel is [Routeable], and the view called after it (HomeViewModel => HomeView) is found by name.
public partial class HomeView : RouteableFor
{
    public HomeView()
    {
        InitializeComponent();
    }

    private async void OnSayHello(object? sender, EventArgs e) => await ((HomeViewModel)BindingContext).SayHelloAsync();
}
