using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;
using Routeables.Abstraction;
using Routeables.Maui;
using Routeables.Regions;
using Routeables.Routing;

namespace Routeables.Test.Maui;

public static class MauiProgram
{
    /// <summary>The name of the second region of the test application.</summary>
    public const string Side = "side";

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
                services.AddRegion(Side);
            });

        return builder.Build();
    }
}
