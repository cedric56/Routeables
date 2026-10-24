using NSubstitute;
using Routeables.Abstraction;
using Routeables.Maui;
using Routeables.Maui.Services;
using Routeables.Services;
using Routeables.Tests.Core;

namespace Routeables.Tests.Maui;

public class MauiRegistrationTests
{
    [Fact]
    public void UseRouteables_registers_the_MAUI_services_and_returns_the_builder()
    {
        var builder = MauiApp.CreateBuilder();

        var returned = Routeables.Maui.Extensions.UseRouteables<FakeMainRouteables>(builder);
        Routeables.Maui.Services.Extensions.UseRouteablesServices(builder);
        using var provider = builder.Services.BuildServiceProvider();

        Assert.Same(builder, returned);
        Assert.NotNull(provider.GetRequiredService<IRouter>());
        Assert.NotNull(provider.GetRequiredService<IInjection>());
        Assert.IsType<RouteableDialogService>(provider.GetRequiredService<IRouteableDialogService>());
        Assert.Same(provider.GetRequiredService<IRouteableDialogService>(), provider.GetRequiredService<RouteableDialogService>());
        Assert.NotNull(provider.GetRequiredService<IRouteableModalService>());
        Assert.IsType<RouteableDialogWindowContext>(provider.GetRequiredService<IRouteableDialogWindowContext>());
        Assert.NotNull(provider.GetRequiredService<IRouteableContentDialogService>());
        Assert.NotNull(provider.GetRequiredService<IRouteableTaskDialogService>());
        Assert.NotNull(provider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(provider.GetRequiredService<IRouteableRequestedThemeService>());
    }

    [Fact]
    public void UseRouteables_alone_registers_the_router_and_the_theme_but_no_dialog_or_notification()
    {
        var builder = MauiApp.CreateBuilder();

        Routeables.Maui.Extensions.UseRouteables<FakeMainRouteables>(builder);
        using var provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRouter>());
        Assert.NotNull(provider.GetRequiredService<IRouteableRequestedThemeService>());
        Assert.Null(provider.GetService<IRouteableDialogService>());
        Assert.Null(provider.GetService<IRouteableModalService>());
        Assert.Null(provider.GetService<IRouteableContentDialogService>());
        Assert.Null(provider.GetService<IRouteableTaskDialogService>());
        Assert.Null(provider.GetService<IRouteableNotificationService>());
    }

    [Fact]
    public void UseRouteablesServices_replaces_a_service_by_the_factory_it_is_given()
    {
        var builder = MauiApp.CreateBuilder();
        var custom = Substitute.For<IRouteableNotificationService>();

        Routeables.Maui.Extensions.UseRouteables<FakeMainRouteables>(builder);
        Routeables.Maui.Services.Extensions.UseRouteablesServices(builder, notificationService: _ => custom);
        using var provider = builder.Services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(provider.GetRequiredService<IRouteableDialogService>());
    }

    private sealed class NullDialogWindowContext : IRouteableDialogWindowContext
    {
        public Page? CurrentPage => null;
    }
}
