using Microsoft.AspNetCore.Components;
using Routeables.Abstraction;
using Routeables.AspNetCore;
using Routeables.Avalonia;
using Routeables.Blazor;
using Routeables.Tests.Core;
using AvaloniaApplication = global::Avalonia.Application;
using Routeables.Abstraction.Registrations;
namespace Routeables.Tests.Hosts;

/// <summary>
/// What every host (Blazor, Avalonia, MAUI, Uno, and WPF through <see cref="HostContract"/>) must give the application, whatever its platform: the same services, the same
/// injection rules and a menu view model that is one instance for the whole host. A host that drifts from the others fails here.
/// </summary>
public class HostContractTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class TestApplication : AvaloniaApplication;

    /// <summary>A host as the contract sees it: how to build its container, and the lifetime its menu view model must have.</summary>
    public sealed record Host(string Name, ServiceLifetime MainLifetime, Func<Action<IServiceCollection>, (IServiceProvider Provider, IDisposable Owner)> Build)
    {
        public override string ToString() => Name;
    }

    private static IServiceProvider Scoped(IServiceCollection services, out IDisposable owner)
    {
        var root = services.BuildServiceProvider();
        var scope = root.CreateScope();
        owner = new Both(scope, root);
        return scope.ServiceProvider;
    }

    private sealed class Both(IDisposable first, IDisposable second) : IDisposable
    {
        public void Dispose() { first.Dispose(); second.Dispose(); }
    }

    public static TheoryData<Host> Hosts() => new()
    {
        new Host("Razor", ServiceLifetime.Scoped, routes =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<NavigationManager>(new TestNavigationManager());
            services.UseRouteablesServer<FakeMenuViewModel>();
            routes(services);
            return (Scoped(services, out var owner), owner);
        }),
        new Host("Avalonia", ServiceLifetime.Singleton, routes =>
        {
            IServiceProvider? provider = null;
            global::Avalonia.AppBuilder.Configure<TestApplication>().UseRouteables<FakeMenuViewModel>(
                configure: routes, afterBuild: p => provider = p);
            return (provider!, (IDisposable)provider!);
        }),
        new Host("Uno", ServiceLifetime.Singleton, routes =>
        {
            IServiceProvider? provider = null;
            Routeables.Uno.UnoRouteablesExtensions.StartRouteables<FakeMenuViewModel>(routes, p => provider = p);
            return (provider!, (IDisposable)provider!);
        }),
        new Host("Maui", ServiceLifetime.Singleton, routes =>
        {
            var builder = MauiApp.CreateBuilder();
            Routeables.Maui.MauiRouteablesExtensions.UseRouteables<FakeMenuViewModel>(builder);
            routes(builder.Services);
            var root = builder.Services.BuildServiceProvider();
            return (root, root);
        }),
    };

    /// <summary>The hosts that route: the ones above and ASP.NET Core, whose services are not compared with the others.</summary>
    public static TheoryData<Host> RoutingHosts()
    {
        var hosts = Hosts();
        hosts.Add(
        new Host("AspNetCore", ServiceLifetime.Scoped, routes =>
            {
                var services = new ServiceCollection();
                services.UseRouteables<FakeMenuViewModel>(routes);
                return (Scoped(services, out var owner), owner);
            }));
        return hosts;
    }

    /// <summary>The routes of the shared scenario: two pages, whatever the host draws them with.</summary>
    public sealed class PageAVm;
    public sealed class PageBVm;

    internal static void RegisterPages(IServiceCollection services)
    {
        services.RegisterRoute<FakeViewFor, PageAVm>(route: "/a");
        services.RegisterRoute<FakeViewFor, PageBVm>(route: "/b");
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Every_host_provides_the_router_and_the_injection_rules(Host host)
    {
        var (provider, owner) = host.Build(_ => { });
        using (owner) HostContract.ProvidesRouterAndInjection(provider);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void The_main_view_model_is_one_instance_for_the_whole_host(Host host)
    {
        var (provider, owner) = host.Build(_ => { });
        using (owner) HostContract.MenuViewModelIsOneInstance(provider);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void A_value_attribute_never_falls_back_to_the_container_on_any_host(Host host)
    {
        var (provider, owner) = host.Build(_ => { });
        using (owner) HostContract.ValueAttributesNeverComeFromTheContainer(provider);
    }

    [Theory]
    [MemberData(nameof(RoutingHosts))]
    public async Task Every_host_routes_the_same_way_back_forward_and_refusals(Host host)
    {
        var (provider, owner) = host.Build(RegisterPages);
        using (owner) await Routeables.Test.RouterContract.RunAsync(provider);
    }
}
