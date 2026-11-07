using Avalonia.Interactivity;
using Routeables.Avalonia;

namespace RouteablesApp;

// A page is a view and a view model. The router sets the view model as the DataContext of any view.
// RouteableFor<T> is the shortest form for a routed page: the generator finds the view by its name (HelloView goes with
// HelloViewModel) and it adds the lifecycle (initialization, disposal) and a typed ViewModel property.
// A plain UserControl works too: any class called ...View is found by the name of its [Routeable] view model.
public partial class HelloView : RouteableFor<HelloViewModel>
{
    public HelloView() => InitializeComponent();

    private async void OnAboutClick(object? sender, RoutedEventArgs e) => await ViewModel.GoToAbout();
}
