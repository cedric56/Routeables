using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Tests.Core;

namespace Routeables.Test.Abstraction;

/// <summary>A module of the tests that is found by its attribute.</summary>
public static class ReflectedModule
{
    [RouteableModule("Reflected")]
    public static IServiceCollection AddReflectedModule(this IServiceCollection services)
    {
        services.RegisterRoute("/reflected", (_, _) => new ReflectedPage());
        services.AddMenuEntries(() => [new RouteableEntry("/reflected", "/reflected", "Reflected")]);
        return services;
    }

    [RouteableModule]
    public static void AddWithAName(IServiceCollection services) => services.RegisterRoute("/named", (_, _) => new ReflectedPage());
}

public sealed class ReflectedPage;

public class RouteableModulesTests
{
    public interface IOrderRepository;
    public sealed class OrderRepository(AppClock clock) : IOrderRepository { public AppClock Clock { get; } = clock; }
    public sealed class AppClock;
    public sealed class OrdersPage(IOrderRepository repository) { public IOrderRepository Repository { get; } = repository; }

    private static IRouteableEntry Entry(string name) => new RouteableEntry("/" + name, "/" + name, name);

    private static ServiceProvider Application(ServiceLifetime main = ServiceLifetime.Singleton, Action<IServiceCollection>? more = null)
    {
        var services = new ServiceCollection();
        services.RegisterMainRouteables<FakeMainRouteables>(main);
        services.AddSingleton<AppClock>();
        services.RegisterRoute("/home", (_, _) => new object());                    // the application has a route of its own
        more?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static void AddOrders(IServiceCollection services)
    {
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.RegisterRoute("/orders", (sp, _) => new OrdersPage(sp.GetRequiredService<IOrderRepository>()));
        services.AddMenuEntries(() => [Entry("orders")]);
    }

    [Fact]
    public void A_route_of_a_module_loaded_after_the_container_was_built_is_reached_with_the_services_of_the_module()
    {
        using var provider = Application();
        var args = new RouteableArgs();
        Assert.Null(provider.GetViewModel("/orders", args));                         // not there yet

        provider.GetRequiredService<IRouteableModules>().Load("Orders", AddOrders);

        var page = Assert.IsType<OrdersPage>(provider.GetViewModel("/orders", args));
        var repository = Assert.IsType<OrderRepository>(page.Repository);
        Assert.Same(provider.GetRequiredService<AppClock>(), repository.Clock);      // the module sees the services of the application
        Assert.Null(provider.GetService<IOrderRepository>());                        // the application does not see the ones of the module
        Assert.NotNull(provider.GetViewModel("/home", args));                        // and its own routes still work
    }

    [Fact]
    public void A_singleton_of_a_module_is_one_instance_for_every_page()
    {
        using var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("Orders", AddOrders);

        var first = (OrdersPage)provider.GetViewModel("/orders", new RouteableArgs())!;
        var second = (OrdersPage)provider.GetViewModel("/orders", new RouteableArgs())!;

        Assert.NotSame(first, second);
        Assert.Same(first.Repository, second.Repository);
    }

    [Fact]
    public void The_routes_of_a_module_can_be_reached_from_an_address()
    {
        using var provider = Application();
        var table = provider.GetRequiredService<RouteableTable>();
        Assert.DoesNotContain(table.Entries, e => e.Template == "/orders");

        var info = provider.GetRequiredService<IRouteableModules>().Load("Orders", AddOrders);

        Assert.Contains(table.Entries, e => e.Template == "/orders");
        Assert.Equal(["/orders"], info.Routes);
    }

    [Fact]
    public void The_entries_of_a_module_reach_the_menu_that_exists_and_the_one_built_later()
    {
        using var provider = Application(ServiceLifetime.Scoped);
        using var early = provider.CreateScope();
        var earlyMain = early.ServiceProvider.GetRequiredService<IMainRouteables>();
        Assert.Empty(earlyMain.Routeables);

        provider.GetRequiredService<IRouteableModules>().Load("Orders", AddOrders);

        Assert.Equal(["orders"], earlyMain.Routeables.Select(e => e.DisplayName));        // added to the menu that was alive

        using var late = provider.CreateScope();
        var lateMain = late.ServiceProvider.GetRequiredService<IMainRouteables>();
        Assert.Equal(["orders"], lateMain.Routeables.Select(e => e.DisplayName));         // and to the one built after
        Assert.NotSame(earlyMain.Routeables[0], lateMain.Routeables[0]);                 // each menu has entries of its own
    }

    [Fact]
    public void Loading_a_module_again_changes_nothing_and_raises_the_event_once()
    {
        using var provider = Application();
        var modules = provider.GetRequiredService<IRouteableModules>();
        var raised = new List<string>();
        modules.ModuleLoaded += (_, module) => raised.Add(module.Name);

        var first = modules.Load("Orders", AddOrders);
        var second = modules.Load("orders", AddOrders);

        Assert.Same(first, second);
        Assert.Equal(["Orders"], raised);
        Assert.True(modules.IsLoaded("ORDERS"));
        Assert.Single(modules.Loaded);
        Assert.Single(provider.GetRequiredService<IMainRouteables>().Routeables);
    }

    [Fact]
    public void A_route_of_the_application_wins_over_the_same_key_of_a_module()
    {
        using var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("Other", services => services.RegisterRoute("/home", (_, _) => "from the module"));

        Assert.IsType<object>(provider.GetViewModel("/home", new RouteableArgs()));
    }

    [Fact]
    public void The_modules_of_an_assembly_are_found_by_their_attribute()
    {
        using var provider = Application();
        var modules = provider.GetRequiredService<IRouteableModules>();

        var loaded = modules.Load(typeof(ReflectedModule).Assembly);

        Assert.Contains(loaded, m => m.Name == "Reflected" && m.Assembly == "Routeables.Test");
        Assert.Contains(loaded, m => m.Name == nameof(ReflectedModule.AddWithAName));       // the method name when the attribute has none
        Assert.IsType<ReflectedPage>(provider.GetViewModel("/reflected", new RouteableArgs()));
        Assert.Contains(provider.GetRequiredService<IMainRouteables>().Routeables, e => e.DisplayName == "Reflected");
    }

    [Fact]
    public void An_assembly_file_is_loaded_with_the_assemblies_of_the_application_shared()
    {
        using var provider = Application();

        var loaded = provider.GetRequiredService<IRouteableModules>().LoadFrom(typeof(ReflectedModule).Assembly.Location);

        Assert.Contains(loaded, m => m.Name == "Reflected");
        Assert.NotNull(provider.GetViewModel("/reflected", new RouteableArgs()));
    }

    [Fact]
    public void An_assembly_without_a_module_is_refused_and_so_is_a_missing_file()
    {
        using var provider = Application();
        var modules = provider.GetRequiredService<IRouteableModules>();

        Assert.Throws<InvalidOperationException>(() => modules.Load(typeof(IRouteableModules).Assembly));
        Assert.Throws<FileNotFoundException>(() => modules.LoadFrom("does-not-exist.dll"));
    }
}
