using Avalonia.Controls;
using Routeables.Core;

namespace RouteablesApp;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}

public partial class MainViewModel(IRouter router) : MainViewModelDefault(router, "/hello/World") { }
