using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Benchmarks;

public sealed class PageA : IRouteableFor { public object? Context { get; set; } }
public sealed class PageB : IRouteableFor { public object? Context { get; set; } }
public sealed class PageDep : IRouteableFor { public object? Context { get; set; } }
public sealed class PageOrder : IRouteableFor { public object? Context { get; set; } }
public sealed class PageGuarded : IRouteableFor { public object? Context { get; set; } }
public sealed class PageDisposable : IRouteableFor { public object? Context { get; set; } }

public sealed class VmA;
public sealed class VmB;

public interface IClock { DateTime Now { get; } }
public sealed class Clock : IClock { public DateTime Now => DateTime.UtcNow; }

/// <summary>A view model with a constructor dependency: what the generated factory (or the reflection fallback) has to resolve.</summary>
public sealed class VmDep(IClock clock) { public IClock Clock { get; } = clock; }

/// <summary>A view model with a route parameter read from the address.</summary>
public sealed class VmOrder(int id) : IRouteable { public int Id { get; } = id; }

/// <summary>A view model with a guard and a routing callback: the lifecycle path of the router.</summary>
public sealed class VmGuarded : IRouteable
{
    public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class VmDisposable : IDisposable
{
    public static int Created;
    public static int Disposed;
    public VmDisposable() => Interlocked.Increment(ref Created);
    public void Dispose() => Interlocked.Increment(ref Disposed);
}

internal static class Bench
{
    /// <summary>
    /// A provider with the routes the benchmarks use, registered as <c>[Routeable]</c> routes are by the generator
    /// (<paramref name="factories"/> = true: constructor factories) or by hand (false: reflection fallback).
    /// </summary>
    public static ServiceProvider Build(bool factories, params string[] regions)
    {
        var services = new ServiceCollection();
        Register(services, factories);
        foreach (var region in regions)
            services.AddRegion(region, ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    public static void Register(IServiceCollection services, bool factories)
    {
        services.RegisterRouting();
        services.AddSingleton<IClock, Clock>();

        const ServiceLifetime transient = ServiceLifetime.Transient;
        if (factories)
        {
            services.RegisterRoute((_, _) => new PageA(), (_, _) => new VmA(), transient, transient, "/a");
            services.RegisterRoute((_, _) => new PageB(), (_, _) => new VmB(), transient, transient, "/b");
            services.RegisterRoute((_, _) => new PageDep(), (sp, _) => new VmDep(sp.GetRequiredService<IClock>()), transient, transient, "/dep");
            services.RegisterRoute((_, _) => new PageGuarded(), (_, _) => new VmGuarded(), transient, transient, "/guarded");
            services.RegisterRoute((_, _) => new PageDisposable(), (_, _) => new VmDisposable(), transient, transient, "/disposable");
            // The id comes from the address ({id:int}); the factory form needs the arguments, so this route keeps the reflection path.
            services.RegisterRoute<PageOrder, VmOrder>(transient, transient, "/orders/{id:int}");
        }
        else
        {
            services.RegisterRoute<PageA, VmA>(transient, transient, "/a");
            services.RegisterRoute<PageB, VmB>(transient, transient, "/b");
            services.RegisterRoute<PageDep, VmDep>(transient, transient, "/dep");
            services.RegisterRoute<PageGuarded, VmGuarded>(transient, transient, "/guarded");
            services.RegisterRoute<PageDisposable, VmDisposable>(transient, transient, "/disposable");
            services.RegisterRoute<PageOrder, VmOrder>(transient, transient, "/orders/{id:int}");
        }
    }

    /// <summary>A benchmark that measures a failed routing measures the wrong thing: fail loudly instead.</summary>
    public static void Ok(RouteableResult result)
    {
        if (!result.Success)
            throw new InvalidOperationException("The routing failed: " + (result.Exception?.Message ?? "no exception"));
    }

    /// <summary>Fills the history of <paramref name="router"/> with <paramref name="count"/> entries.</summary>
    public static async Task FillAsync(IRouter router, int count)
    {
        for (var i = 0; i < count; i++)
            Ok(await router.GoToAsync(i % 2 == 0 ? "/a" : "/b"));
    }
}
