using Microsoft.Extensions.DependencyInjection;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using AvaloniaApplication = global::Avalonia.Application;
using Routeables.Tests.Core;

namespace Routeables.Tests.Avalonia;

public class DefaultMessageBoxServiceTests
{
    private sealed class TestApplication : AvaloniaApplication;

    [Fact]
    public void UseRouteables_without_a_message_box_service_registers_the_default_one()
    {
        IServiceProvider? provider = null;

        global::Avalonia.AppBuilder.Configure<TestApplication>().UseRouteables<FakeMainRouteables>(
            configure: services => services.UseRouteablesServices(string.Empty),
            afterBuild: services => provider = services);

        var serviceProvider = Assert.IsAssignableFrom<IServiceProvider>(provider);
        using (serviceProvider as IDisposable)
        {
            Assert.IsType<DefaultMessageBoxService>(serviceProvider.GetRequiredService<IMessageBoxService>());
        }
    }
}
