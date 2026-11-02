using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>An arguments object given to the router is a model: the router never writes its state on the caller's instance.</summary>
public class RouteableArgsIsolationTests
{
    public sealed class IsoView : IRouteableFor { public object? Context { get; set; } }
    public sealed class IsoVm;

    [Fact]
    public async Task The_same_arguments_used_for_two_route_changes_keep_no_state_of_the_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<IsoView, IsoVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/iso");
        services.AddTransient<IsoVm>();
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var args = RouteableArgs.Create();
        var seen = new List<IRouteableArgs>();
        router.Routing += (_, e) => seen.Add(e.To.Args);

        await router.GoToAsync("/iso", args, cancellationToken: ct);
        await router.GoToAsync("/iso", args, cancellationToken: ct);

        Assert.Null(args.Route);
        Assert.Null(args.Router);
        Assert.Null(args.Exception);
        Assert.Empty(args.Values);
        Assert.Equal(2, seen.Count);
        Assert.NotSame(seen[0], seen[1]);
        Assert.NotSame(args, seen[0]);
    }
}
