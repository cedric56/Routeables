using Avalonia.Controls;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using Routeables.Services;
using AvaloniaApplication = global::Avalonia.Application;
using Routeables.Tests.Core;

namespace Routeables.Tests.Avalonia;

// ServiceLocator.Services is one static for the process: the classes that read or set it must not run at the same time.
[Collection("Avalonia ServiceLocator")]
public class RouteableBuilderTests
{
    private sealed class MessageBoxService : IMessageBoxService
    {
        public bool IsDisplayable(TopLevel topLevel) => throw new NotImplementedException();

        public Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params, TopLevel? topLevel = null)
            => throw new NotImplementedException();

        public Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, TopLevel? topLevel = null, params RouteableTaskDialogResult[] results)
            => throw new NotImplementedException();

        public Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, TopLevel? topLevel = null)
            => throw new NotImplementedException();
    }

    private sealed class TestApplication : AvaloniaApplication;

    private static IServiceProvider Build(IMessageBoxService? messageBoxService = null)
    {
        IServiceProvider? provider = null;

        var builder = global::Avalonia.AppBuilder.Configure<TestApplication>().UseRouteables<FakeMainViewModel>(
            configure: services => services.UseRouteablesServices(messageBoxService is null ? null : _ => messageBoxService, icon: string.Empty),
            afterBuild: services => provider = services);

        Assert.NotNull(builder);
        return Assert.IsAssignableFrom<IServiceProvider>(provider);
    }

    [Fact]
    public void UseRouteables_registers_the_Avalonia_services_before_returning_the_builder()
    {
        var serviceProvider = Build(new MessageBoxService());
        using (serviceProvider as IDisposable)
        {
            Assert.IsType<RouteableRequestThemeService>(serviceProvider.GetRequiredService<IRouteableRequestedThemeService>());
            Assert.NotNull(serviceProvider.GetRequiredService<IRouteableNotificationService>());
            Assert.NotNull(serviceProvider.GetRequiredService<IRouter>());
        }
    }

    [Fact]
    public void A_message_box_service_passed_by_the_caller_is_the_one_registered()
    {
        var custom = new MessageBoxService();
        var serviceProvider = Build(custom);
        using (serviceProvider as IDisposable)
        {
            Assert.Same(custom, serviceProvider.GetRequiredService<IMessageBoxService>());
        }
    }

    [Fact]
    public void Without_a_message_box_service_the_default_implementation_is_registered()
    {
        var serviceProvider = Build();
        using (serviceProvider as IDisposable)
        {
            Assert.IsType<DefaultMessageBoxService>(serviceProvider.GetRequiredService<IMessageBoxService>());
        }
    }

    [Fact]
    public void UseRouteables_configures_an_AppBuilder_without_an_app_factory()
    {
        IServiceProvider? provider = null;

        var builder = global::Avalonia.AppBuilder.Configure<TestApplication>()
            .UseRouteables<FakeMainViewModel>(
                configure: services => services.UseRouteablesServices(icon: string.Empty),
                afterBuild: services => provider = services);

        Assert.NotNull(builder);
        using (provider as IDisposable)
        {
            Assert.NotNull(provider!.GetRequiredService<IRouter>());
            Assert.IsType<DefaultMessageBoxService>(provider!.GetRequiredService<IMessageBoxService>());
        }
    }

    [Fact]
    public void UseRouteables_runs_the_external_configuration_before_the_provider_is_built()
    {
        IServiceProvider? provider = null;
        var custom = new MessageBoxService();

        global::Avalonia.AppBuilder.Configure<TestApplication>().UseRouteables<FakeMainViewModel>(
            configure: services =>
            {
                services.AddSingleton<Marker>();
                services.UseRouteablesServices(_ => custom, icon: string.Empty);
            },
            afterBuild: services => provider = services);

        using (provider as IDisposable)
        {
            Assert.NotNull(provider!.GetRequiredService<Marker>());
            Assert.Same(custom, provider!.GetRequiredService<IMessageBoxService>());
        }
    }

    [Fact]
    public void UseRouteables_registers_the_router_the_theme_and_the_default_dialogs_and_notifications()
    {
        IServiceProvider? provider = null;

        global::Avalonia.AppBuilder.Configure<TestApplication>()
            .UseRouteables<FakeMainViewModel>(afterBuild: services => provider = services);

        using (provider as IDisposable)
        {
            Assert.NotNull(provider!.GetRequiredService<IRouter>());
            Assert.IsType<RouteableRequestThemeService>(provider!.GetRequiredService<IRouteableRequestedThemeService>());
            Assert.IsType<DefaultMessageBoxService>(provider!.GetRequiredService<IMessageBoxService>());
            Assert.NotNull(provider!.GetRequiredService<IRouteableDialogService>());
            Assert.NotNull(provider!.GetRequiredService<IRouteableNotificationService>());
        }
    }

    [Fact]
    public void GetRouteableServices_fails_when_no_host_runs()
    {
        var application = new TestApplication();
        var previous = ServiceLocator.Services;
        ServiceLocator.Services = null;
        try
        {
            Assert.Throws<InvalidOperationException>(() => application.GetRouteableServices());
        }
        finally
        {
            ServiceLocator.Services = previous;
        }
    }

    private sealed class Marker;
}
