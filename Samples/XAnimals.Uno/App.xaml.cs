using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using Routeables.Uno;
using XAnimals.Uno.Views;
using XAnimals.ViewModels;
using XAnimals.Common;

namespace XAnimals.Uno;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

        // The container is built now, so it is ready when OnLaunched creates the window.
        Services = this.UseRouteables<MainViewModel>(services =>
        {
            services.AddGeneratedRoutes();          // the routes declared by attributes on the views of this project
            AnimalIcons.Register();                 // the glyph of each page of the menu
            services.RegisterViewModels();
        });
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = this.OpenMainWindow(new MainPage(), window =>
        {
#if !__IOS__
            window.Title = "XAnimals.Uno";      // Window.Title is not implemented on iOS
#endif
        });
    }
}
