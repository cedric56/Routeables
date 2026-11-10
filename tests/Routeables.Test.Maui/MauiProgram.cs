using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;
using Routeables.Abstraction;
using Routeables.Abstraction.Registrations;
using Routeables.Maui;
using Routeables.Routing;

namespace Routeables.Test.Maui;

/// <summary>The entry point of the test application: it is a real Maui app, hosting the visual test runner and the routes under test.</summary>
public static class MauiProgram
{
    /// <summary>The name of the second region of the test application.</summary>
    public const string Side = "side";

    /// <summary>
    /// Builds the application of the tests: the visual runner that runs them, the UI-testing setup that puts them on the thread of the
    /// interface, and the Routeables shell with the two pages and the side region the tests navigate to.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseVisualTestRunner(conf => conf
                .AddCliConfiguration()                      // `dotnet test -f net10.0-...` talks to the application through it
                .AddConsoleResultChannel()
                .AddTestAssembly(typeof(MauiProgram).Assembly)
                .AddXunit3())
            .ConfigureUITesting()                           // [UIFact] and [UITheory] run on the thread of the interface
            .UseRouteables<TestMain>(services =>
            {
                services.RegisterRoute<PageAView, PageAViewModel>(route: "/a");
                services.RegisterRoute<PageBView, PageBViewModel>(route: "/b");
                services.RegisterRoute<VisualFormView, VisualFormViewModel>(route: "/visual-form");
                services.RegisterRoute<VisualAboutView, VisualAboutViewModel>(route: "/visual-about");
                services.AddRegion(Side);
            });

        return builder.Build();
    }
}