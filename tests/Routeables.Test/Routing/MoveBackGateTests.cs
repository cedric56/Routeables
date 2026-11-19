using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Routing;

/// <summary>Going back several pages, or to a page, is worked out once the route change may start, not when it is asked.</summary>
public class MoveBackGateTests
{

    private sealed class SlowGuard : IRouteGuard
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
        {
            if (change.To.ViewModel is MbCVm)
            {
                Asked.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            return RouteGuardDecision.Allow;
        }
    }

    private static ServiceProvider Build(SlowGuard guard)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<MbAView, MbAVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/mb-a");
        services.RegisterRoute<MbBView, MbBVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/mb-b");
        services.RegisterRoute<MbCView, MbCVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/mb-c");
        services.AddTransient<MbAVm>();
        services.AddTransient<MbBVm>();
        services.AddTransient<MbCVm>();
        services.AddSingleton<IRouteGuard>(guard);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Going_back_two_pages_counts_from_the_page_that_is_shown_when_the_move_starts()
    {
        var ct = TestContext.Current.CancellationToken;
        var guard = new SlowGuard();
        using var provider = Build(guard);
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/mb-a", cancellationToken: ct);
        await router.GoToAsync("/mb-b", cancellationToken: ct);

        var slow = router.GoToAsync("/mb-c", cancellationToken: ct);          // A, B, then C once the guard lets it
        await guard.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        var back = router.GoBackAsync(2, ct); // asked while C is not entered yet: counted from C, so A
        guard.Release.SetResult();
        await slow;
        await back;

        // Two pages back from C is A. Worked out when asked (from B), it would be "before A" (cancelled), or wrong with one page.
        Assert.IsType<MbAVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Going_back_to_a_page_looks_at_the_history_that_exists_when_the_move_starts()
    {
        var ct = TestContext.Current.CancellationToken;
        var guard = new SlowGuard();
        using var provider = Build(guard);
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/mb-a", cancellationToken: ct);

        var slow = router.GoToAsync("/mb-c", cancellationToken: ct);
        await guard.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        var back = router.GoBackToAsync("/mb-a", ct);                         // only A is in the history when it is asked, and C is not there yet
        guard.Release.SetResult();
        await slow;
        var result = await back;

        Assert.True(result.Success);
        Assert.IsType<MbAVm>(router.CurrentViewModel);
    }
}
