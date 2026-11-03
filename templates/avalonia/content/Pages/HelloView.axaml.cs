using Avalonia.Interactivity;
using Routeables.Avalonia;

namespace RouteablesApp;

// A page is a view and a view model. The router sets the view model as the DataContext of any view.
// RouteableFor<T> is the shortest form for a routed page: AddGeneratedRoutes() finds the view by its name (HelloView goes with
// HelloViewModel) and it adds the lifecycle (initialization, disposal) and a typed ViewModel property.
// A plain UserControl works too if you register it yourself: services.RegisterRoute<MyView, MyViewModel>("/my").
public partial class HelloView : RouteableFor<HelloViewModel>
{
    public HelloView() => InitializeComponent();

    private async void OnAboutClick(object? sender, RoutedEventArgs e) => await ViewModel.GoToAbout();
}
