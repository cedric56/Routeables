using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using Routeables.Uno;
using Routeables.Uno.Services;
using XAnimals.Uno.Views;
using XAnimals.ViewModels;

namespace XAnimals.Uno;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

        // The container is built now, so it is ready when OnLaunched creates the window.
        Services = this.UseRouteables(services =>
        {
            services.UseRouteablesServices();       // dialogs, content dialogs, task dialogs, notifications
            services.AddGeneratedRoutes();          // the routes declared by attributes on the views of this project
            AnimalIcons.Register();                 // the glyph of each page of the menu
            services.RegisterViewModels<CatDetailView, DogDetailView>();
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
