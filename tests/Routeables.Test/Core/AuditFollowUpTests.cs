using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Locking;
using Routeables.Regions;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Core;

namespace Routeables.Tests.Core;

/// <summary>Faults found by a review of the failure paths: each of these failed before it was fixed.</summary>
public class AuditFollowUpTests
{
    private static void Fails(Action action)
    {
        try { action(); }
        catch (Exception) { return; }
        Assert.Fail("An exception was expected.");
    }

    // ---- modules ----------------------------------------------------------------------------------------------------------------

    public sealed class Wanted;
    public sealed class AsksForProvider(IServiceProvider provider) { public IServiceProvider Provider { get; } = provider; }
    public sealed class Tracked(List<string> log, string name, bool fails = false) : IDisposable
    {
        public void Dispose() { log.Add(name); if (fails) throw new InvalidOperationException(name); }
    }

    private static ServiceProvider Application()
    {
        var services = new ServiceCollection();
        services.RegisterMenuViewModel<FakeMenuViewModel>(ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void A_service_of_a_module_that_asks_for_the_provider_gets_the_one_of_the_module()
    {
        using var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("M", services =>
        {
            services.AddSingleton<Wanted>();
            services.AddSingleton<AsksForProvider>();
            services.RegisterRoute("/asks", (sp, _) => sp.GetRequiredService<AsksForProvider>());
        });

        var asks = (AsksForProvider)provider.GetViewModel("/asks", new RouteableArgs())!;

        Assert.NotNull(asks.Provider.GetService<Wanted>());       // a service only the module registered
    }

    [Fact]
    public void A_module_service_that_fails_to_dispose_does_not_keep_the_others_alive()
    {
        var log = new List<string>();
        var provider = Application();
        provider.GetRequiredService<IRouteableModules>().Load("N", services =>
        {
            services.AddSingleton(_ => new Tracked(log, "second", fails: true));
            services.RegisterRoute("/u", (sp, _) => sp.GetRequiredService<Tracked>());
        });
        provider.GetRequiredService<IRouteableModules>().Load("M", services =>
        {
            services.AddSingleton(_ => new Tracked(log, "first"));
            services.RegisterRoute("/t", (sp, _) => sp.GetRequiredService<Tracked>());
        });
        _ = provider.GetViewModel("/t", new RouteableArgs());
        _ = provider.GetViewModel("/u", new RouteableArgs());

        Fails(() => provider.Dispose());

        Assert.Contains("first", log);        // the module that comes after the one that failed was still disposed
        Assert.Contains("second", log);
    }

    // ---- form lock --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_subscriber_that_throws_on_lock_does_not_leave_the_form_locked_for_ever()
    {
        var broker = new RouteableLockBroker();
        var formLock = new RouteableFormLock(broker);
        var fail = true;
        broker.Subscribe("LockChanged", _ => { if (fail) throw new InvalidOperationException("subscriber"); });

        Fails(() => formLock.Lock());
        Assert.False(formLock.IsLocked);

        fail = false;
        using var token = formLock.Lock();
        Assert.True(formLock.IsLocked);
    }

    // ---- parser -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_catch_all_value_must_satisfy_its_constraint_as_the_other_placeholders_do()
    {
        Assert.Equal("/files/12", "/" + RouteParser.Build("files/{**path:int}", new Dictionary<string, object?> { ["path"] = "12" }).TrimStart('/'));
        Assert.Throws<ArgumentException>(() => RouteParser.Build("files/{**path:int}", new Dictionary<string, object?> { ["path"] = "not-an-int" }));
        Assert.Throws<ArgumentException>(() => RouteParser.Build("files/{**path:nonsense}", new Dictionary<string, object?> { ["path"] = "a/b" }));
        Assert.Equal("files/a/b", RouteParser.Build("files/{**path}", new Dictionary<string, object?> { ["path"] = "a/b" }).TrimStart('/'));
    }

    // ---- Blazor address comparison ----------------------------------------------------------------------------------------------

    [Fact]
    public void The_values_of_the_query_are_data_and_the_path_is_not()
    {
        Assert.False(RouteableUrlSync.Same("/search?q=ABC", "/search?q=abc"));
        Assert.False(RouteableUrlSync.Same("/search?path=folder/", "/search?path=folder"));
        Assert.True(RouteableUrlSync.Same("/Search?Q=abc", "/search?q=abc"));
        Assert.True(RouteableUrlSync.Same("/orders/", "/orders"));
        Assert.True(RouteableUrlSync.Same("/dogs/Afghan%20Hound", "/dogs/Afghan Hound"));
    }

    // ---- RouteableReturn --------------------------------------------------------------------------------------------------------

    [Fact]
    public void RouteableReturn_follows_the_lifetime_of_the_host_so_each_visitor_has_its_own()
    {
        var scoped = new ServiceCollection();
        scoped.TryAddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));
        scoped.AddRouteableReturn();
        using var visitors = scoped.BuildServiceProvider(validateScopes: true);
        using var a = visitors.CreateScope();
        using var b = visitors.CreateScope();
        Assert.NotSame(a.ServiceProvider.GetRequiredService<RouteableReturn>(), b.ServiceProvider.GetRequiredService<RouteableReturn>());
        Assert.Same(a.ServiceProvider.GetRequiredService<RouteableReturn>(), a.ServiceProvider.GetRequiredService<RouteableReturn>());

        var single = new ServiceCollection();
        single.AddRouteableReturn();
        using var application = single.BuildServiceProvider();
        Assert.Same(application.GetRequiredService<RouteableReturn>(), application.GetRequiredService<RouteableReturn>());
    }

    // ---- view cache -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_view_that_fails_to_dispose_does_not_keep_the_others_of_the_cache_alive()
    {
        var log = new List<string>();
        var cache = new ScopedViewCache();
        cache.GetOrCreate("a", () => new Tracked(log, "a"));
        cache.GetOrCreate("b", () => new Tracked(log, "b", fails: true));
        cache.GetOrCreate("c", () => new Tracked(log, "c"));

        Fails(cache.Dispose);

        Assert.Equal(3, log.Count);
    }
}
