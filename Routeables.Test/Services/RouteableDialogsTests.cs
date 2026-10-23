using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Services;

namespace Routeables.Tests.Services;

public class RouteableDialogsTests
{
    // A service nobody calls: the tests only look at which instance is handed back.
    public class Stub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }

    private static T Make<T>() where T : class => DispatchProxy.Create<T, Stub>();

    [Fact]
    public void The_three_services_are_reached_through_one_injection()
    {
        var dialog = Make<IRouteableDialogService>();
        var content = Make<IRouteableContentDialogService>();
        var task = Make<IRouteableTaskDialogService>();
        var services = new ServiceCollection();
        services.AddSingleton(dialog);
        services.AddSingleton(content);
        services.AddSingleton(task);
        services.AddRouteableDialogs();
        using var provider = services.BuildServiceProvider();

        var dialogs = provider.GetRequiredService<IRouteableDialogs>();

        Assert.Same(dialog, dialogs.MessageBox);
        Assert.Same(content, dialogs.ContentDialog);
        Assert.Same(task, dialogs.TaskDialog);
    }

    [Fact]
    public void A_service_replaced_afterwards_is_the_one_returned()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Make<IRouteableDialogService>());
        services.AddRouteableDialogs();
        var replacement = Make<IRouteableDialogService>();
        services.AddSingleton(replacement);   // a Reshape registers its own after the host's
        using var provider = services.BuildServiceProvider();

        Assert.Same(replacement, provider.GetRequiredService<IRouteableDialogs>().MessageBox);
    }

    [Fact]
    public void Calling_it_twice_registers_it_once()
    {
        var services = new ServiceCollection();
        services.AddRouteableDialogs().AddRouteableDialogs();

        Assert.Single(services, d => d.ServiceType == typeof(IRouteableDialogs));
    }
}
