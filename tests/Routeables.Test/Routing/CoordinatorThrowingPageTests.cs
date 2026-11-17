using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Segments;
namespace Routeables.Tests.Routing;

/// <summary>An absolute coordination erases the histories first: a page that throws must not leave the regions empty.</summary>
public class CoordinatorThrowingPageTests
{
    public sealed class CtHomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class CtHomeVm;
    public sealed class CtBoomView : IRouteableFor { public object? Context { get; set; } }
    public sealed class CtBoomVm : IRouteable
    {
        public Task OnEnteringAsync(IRouteableArgs args, CancellationToken cancellationToken = default) => throw new InvalidOperationException("the page failed");
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRouteCoordinator();
        services.RegisterRoute<CtHomeView, CtHomeVm>(route: "/ct-home");
        services.RegisterRoute<CtBoomView, CtBoomVm>();
        services.RegisterSegment<CtBoomVm>("Boom");
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_page_that_throws_leaves_the_page_that_was_shown_and_the_exception_reaches_the_caller()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/ct-home", cancellationToken: ct);
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();

        RouteableResult? result = null;
        Exception? thrown = null;
        try { result = await coordinator.CoordinateAsync(RouteCoordination.Parse("/Boom"), ct); }
        catch (Exception ex) { thrown = ex; }

        Assert.True(thrown is not null || result is { Success: false });
        Assert.Equal(1, router.History.Count);
        Assert.IsType<CtHomeVm>(router.CurrentViewModel);
    }
}
