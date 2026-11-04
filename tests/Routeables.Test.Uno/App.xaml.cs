using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Uno;

namespace Routeables.Test.Uno;

/// <summary>The Uno test application: builds the Routeables container, registers the test routes and side region, and opens the runner page.</summary>
public partial class App : Application
{
    /// <summary>The name of the second region of the test application.</summary>
    public const string Side = "side";

    /// <summary>Configures Routeables with the test main view model, registers routes and the side region, and sets the window title.</summary>
    public App()
    {
        InitializeComponent();

        // The container is built now, the window is opened as the application runs: the page is the runner above a zone the tests fill.
        Services = this.UseRouteables<TestMain>(
            services =>
            {
                services.RegisterRoute<PageAView, PageAViewModel>(route: "/a");
                services.RegisterRoute<PageBView, PageBViewModel>(route: "/b");
                services.AddRegion(Side);
            },
            () => new MainPage(),
            window => window.Title = "Routeables.Test.Uno");
    }

    /// <summary>The service provider built by <see cref="Routeables.Uno.Extensions.UseRouteables{TMain}(Application, Action{Microsoft.Extensions.DependencyInjection.IServiceCollection}, Func{FrameworkElement}, Action{Window}?, Action{IServiceProvider}?, Action?)"/>.</summary>
    public IServiceProvider Services { get; }
}