using Microsoft.Extensions.DependencyInjection;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using AvaloniaApplication = global::Avalonia.Application;

namespace Routeables.Tests.Avalonia;

public class DefaultMessageBoxServiceTests
{
    private sealed class TestApplication : AvaloniaApplication;

    [Fact]
    public void CreateRouteableApp_without_a_message_box_service_registers_the_default_one()
    {
        IServiceProvider? provider = null;

        RouteableBuilder.CreateRouteableApp<TestApplication>(
            appFactory: _ => new TestApplication(),
            configureExternalServices: services => services.UseRouteablesServices(string.Empty),
            afterBuild: services => provider = services);

        var serviceProvider = Assert.IsAssignableFrom<IServiceProvider>(provider);
        using (serviceProvider as IDisposable)
        {
            Assert.IsType<DefaultMessageBoxService>(serviceProvider.GetRequiredService<IMessageBoxService>());
        }
    }
}
