using Routeables.Generated;
using Routeables.Uno;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Uno;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // The container is built now and the main window is created as the application runs: there is no OnLaunched to write.
        Services = this.UseRouteables<MainViewModelWithTabs>(
            services =>
            {
                services.AddGeneratedRoutes();          // the routes declared by attributes on the views of this project
                AnimalIcons.Register();                 // the glyph of each page of the menu
                services.RegisterViewModels();
            },
            () => new MainPage(),
            window =>
            {

#if WINDOWS
            var windowHandle = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("Assets/icon.ico");
#endif

#if !__IOS__
                window.Title = "XAnimals.Uno";      // Window.Title is not implemented on iOS
#endif
            });

        LinkInbox.Instance.Attach(Services);        // the links received by the platform are followed once the first page is shown
    }

    public IServiceProvider Services { get; }
}
