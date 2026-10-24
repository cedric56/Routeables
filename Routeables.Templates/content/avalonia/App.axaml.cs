using Avalonia;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using Routeables.Generated;
using RouteablesApp.ViewModels;
using RouteablesApp.Views;

namespace RouteablesApp;

public partial class App : Application
{
    // The usual Avalonia shape: the application has its own constructor and Routeables is added to the builder.
    public static AppBuilder CreateApp() => AppBuilder.Configure<App>().UseRouteables<MainViewModel>(services =>   // the router and the menu
    {
        services.UseRouteablesServices("avares://RouteablesApp/Assets/icon.ico");   // optional: dialogs and notifications
        services.AddGeneratedRoutes();                                              // the routes declared by attributes
    });

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        this.OpenMainView(new MainWindow());     // the window gets the menu as its DataContext

        base.OnFrameworkInitializationCompleted();
    }
}
