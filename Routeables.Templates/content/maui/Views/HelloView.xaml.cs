using Routeables.Maui;
using RouteablesApp.ViewModels;

namespace RouteablesApp.Views;

public partial class HelloView : RouteableFor
{
    public HelloView()
    {
        InitializeComponent();
    }

    private async void OnBack(object? sender, EventArgs e) => await ((HelloViewModel)BindingContext).BackAsync();
}
