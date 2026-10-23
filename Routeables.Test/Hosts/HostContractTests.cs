using Avalonia.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Core;
using Routeables.Razor;
using Routeables.Locking;
using Routeables.Tests.Core;
using AvaloniaApplication = global::Avalonia.Application;

namespace Routeables.Tests.Hosts;

/// <summary>
/// What every host (Blazor, Avalonia, MAUI) must give the application, whatever its platform: the same services, the same
/// injection rules and a main view model that is one instance for the whole host. A host that drifts from the others fails here.
/// </summary>
public class HostContractTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class TestApplication : AvaloniaApplication;

    /// <summary>A host as the contract sees it: how to build its container, and the lifetime its main view model must have.</summary>
    public sealed record Host(string Name, ServiceLifetime MainLifetime, Func<(IServiceProvider Provider, IDisposable Owner)> Build)
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
        new Host("Razor", ServiceLifetime.Scoped, () =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<NavigationManager>(new TestNavigationManager());
            services.UseRouteables(Runtime.BlazorServer);
            services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Scoped);
            return (Scoped(services, out var owner), owner);
        }),
        new Host("Avalonia", ServiceLifetime.Singleton, () =>
        {
            IServiceProvider? provider = null;
            RouteableBuilder.CreateRouteableApp<TestApplication>(
                _ => new TestApplication(),
                configureExternalServices: s => s.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Singleton),
                afterBuild: p => provider = p);
            return (provider!, (IDisposable)provider!);
        }),
        new Host("Maui", ServiceLifetime.Singleton, () =>
        {
            var builder = MauiApp.CreateBuilder();
            Routeables.Maui.Extensions.UseRouteables(builder);
            builder.Services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Singleton);
            var root = builder.Services.BuildServiceProvider();
            return (root, root);
        }),
    };

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Every_host_provides_the_router_and_the_injection_rules(Host host)
    {
        var (provider, owner) = host.Build();
        using (owner)
        {
            Assert.NotNull(provider.GetRequiredService<IRouter>());
            Assert.NotNull(provider.GetRequiredService<IInjection>());
            // Locking is optional: no host registers the broker or the lock coordinator (AddLocking does).
            Assert.Null(provider.GetService<IRouteableBrokerService>());
        }
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void The_main_view_model_is_one_instance_for_the_whole_host(Host host)
    {
        var (provider, owner) = host.Build();
        using (owner)
        {
            Assert.Same(provider.GetRequiredService<IMainRouteables>(), provider.GetRequiredService<IMainRouteables>());
        }
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void A_value_attribute_never_falls_back_to_the_container_on_any_host(Host host)
    {
        var (provider, owner) = host.Build();
        using (owner)
        {
            var valueAttributes = new[] { "ParameterAttribute", "RouteableParameterAttribute", "QueryPropertyAttribute" };

            foreach (var entry in provider.GetRequiredService<IInjection>().AttributeTypes)
                if (valueAttributes.Contains(entry.Type.Name))
                    Assert.True(!entry.FromContainer);
        }
    }
}
