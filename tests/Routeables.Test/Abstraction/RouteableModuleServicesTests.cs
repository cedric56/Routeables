using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Routing;
using Routeables.Tests.Core;

namespace Routeables.Test.Abstraction;

/// <summary>The services of a module loaded while running: lifetimes, several registrations of a type, disposal.</summary>
public class RouteableModuleServicesTests
{
    public interface IPlugin;
    public sealed class PluginA : IPlugin;
    public sealed class PluginB : IPlugin;
    public sealed class UnitOfWork;
    public sealed class Connection : IDisposable { public bool Disposed { get; private set; } public void Dispose() => Disposed = true; }
    public sealed class AsyncConnection : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return default; }
    }

    private static ServiceProvider Application()
    {
        var services = new ServiceCollection();
        services.RegisterMenuViewModel<FakeMenuViewModel>(ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    private static T Get<T>(IServiceProvider provider, string route) where T : class
        => (T)provider.GetViewModel(route, new RouteableArgs())!;

    [Fact]
    public void A_scoped_service_of_a_module_is_one_instance_for_the_life_of_the_module_as_the_guide_says()
    {
        using var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("M", services =>
        {
            services.AddScoped<UnitOfWork>();
            services.AddTransient<Connection>();
            services.RegisterRoute("/uow", (sp, _) => new[] { sp.GetRequiredService<UnitOfWork>(), sp.GetRequiredService<UnitOfWork>() });
            services.RegisterRoute("/conn", (sp, _) => new[] { sp.GetRequiredService<Connection>(), sp.GetRequiredService<Connection>() });
        });

        var uow = Get<UnitOfWork[]>(provider, "/uow");
        Assert.Same(uow[0], uow[1]);

        var connections = Get<Connection[]>(provider, "/conn");
        Assert.NotSame(connections[0], connections[1]);        // transient: one for each request
    }

    [Fact]
    public void All_the_registrations_of_a_type_answer_IEnumerable_and_the_last_one_answers_the_single_request()
    {
        using var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("M", services =>
        {
            services.AddSingleton<IPlugin, PluginA>();
            services.AddSingleton<IPlugin, PluginB>();
            services.RegisterRoute("/all", (sp, _) => sp.GetServices<IPlugin>().ToArray());
            services.RegisterRoute("/one", (sp, _) => sp.GetRequiredService<IPlugin>());
        });

        var all = Get<IPlugin[]>(provider, "/all");
        Assert.Equal(2, all.Length);
        Assert.IsType<PluginA>(all[0]);
        Assert.IsType<PluginB>(all[1]);
        Assert.IsType<PluginB>(Get<IPlugin>(provider, "/one"));
        Assert.Same(all[1], Get<IPlugin>(provider, "/one"));       // the same singleton in both
    }

    [Fact]
    public void The_services_a_module_built_once_are_disposed_with_it_including_the_ones_that_only_have_DisposeAsync()
    {
        var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("M", services =>
        {
            services.AddSingleton<Connection>();
            services.AddSingleton<AsyncConnection>();
            services.RegisterRoute("/both", (sp, _) => new object[] { sp.GetRequiredService<Connection>(), sp.GetRequiredService<AsyncConnection>() });
        });
        var built = Get<object[]>(provider, "/both");

        provider.Dispose();

        Assert.True(((Connection)built[0]).Disposed);
        Assert.True(((AsyncConnection)built[1]).Disposed);
    }
}
