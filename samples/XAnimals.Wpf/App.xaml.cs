using System.Windows;
using System.Windows.Media.Imaging;
using Routeables.Generated;
using Routeables.Services;
using Routeables.Wpf;
using Routeables.Wpf.Services;
using XAminals.Models;
using XAnimals.Common;
using XAnimals.ViewModels;
using XAnimals.Wpf.Views;

namespace XAnimals.Wpf;

public partial class App : Application
{
    // The assembly is named in the address: the resource is looked for in this assembly, whatever the application assembly is when it is built or hosted.
    private const string IconUri = "pack://application:,,,/XAnimals.Wpf;component/Assets/icon.ico";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The images of the lists are loaded now, one after the other, while the application starts: a page finds them in memory.
        _ = CachedImageHandler.PreloadAsync(AnimalPicture.Http, AnimalImages.Thumbnails);

        // The container is built now and the main window is opened as the application runs; the dialogs and notifications are those of Routeables.Wpf.
        this.UseRouteables<MainViewModelWithTabs>(
            services =>
            {
                services.AddGeneratedRoutes();      // the routes declared by attributes on the view models, paired with the views of this project and of the modules it references
                AnimalIcons.Register();             // the glyph of each page of the menu
                services.RegisterViewModels();      // shared with the Avalonia, MAUI, Uno and Blazor samples
                services.UseRouteablesServices(icon: IconUri);   // the icon of the dialogs
            },
            () => new MainView(),
            window =>
            {
                window.Title = "XAnimals.Wpf";
                window.Icon = BitmapFrame.Create(new Uri(IconUri));
                window.Width = 1100;
                window.Height = 720;
            });

        // An address given on the command line (xanimals.exe xanimals://dogs/Eurohound) is followed once the first page is shown.
        if (e.Args.Length > 0 && Uri.TryCreate(e.Args[0], UriKind.Absolute, out var link))
        {
            Dispatcher.BeginInvoke(
                async () => await RouteableDeepLinks.GoToAsync(this.GetRouteableServices(), link, ["xanimals"]),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }
}
