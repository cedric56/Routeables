using Microsoft.UI;
using Microsoft.UI.Xaml;
using Routeables.Generated;
using Routeables.WinUI3;
using WinRT.Interop;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.WinUI3;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // The container is built now; the window is opened by OnLaunched.
        Services = this.UseRouteables<MainViewModelWithTabs>(services =>
        {
            services.AddGeneratedRoutes();          // the routes declared by attributes on the views of this project
            AnimalIcons.Register();                 // the glyph of each page of the menu
            services.RegisterViewModels();
            services.WithAppNotifications();        // the System button of the Dialogs page: the notification center of Windows
        });

        LinkInbox.Instance.Attach(Services);        // the links received are followed once the first page is shown
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args) =>
        this.OpenMainWindow(new MainPage(), window =>
        {
#if WINDOWS
            var windowHandle = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));
#endif

            window.Title = "XAnimals.WinUI3";
        });
}
