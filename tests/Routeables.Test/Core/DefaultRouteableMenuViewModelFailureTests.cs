using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Core;

/// <summary>An application without a menu whose root cannot be opened: the failure is reported, and the next appearance of the region starts again.</summary>
// The failure goes to RouteableErrors.Default, which is process-wide: the classes that replace it or read it run one after the other.
[Collection("RouteableErrors.Default")]
public class DefaultRouteableMenuViewModelFailureTests
{
    public sealed class ThrowingVm
    {
        public ThrowingVm() => throw new InvalidOperationException("the root cannot be built");
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static ServiceProvider Build(Action<IServiceCollection> routes)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        routes(services);
        services.RegisterMenuViewModel<DefaultRouteableMenuViewModel>(ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_root_that_is_not_registered_can_be_started_again_once_the_first_try_failed()
    {
        using var provider = Build(services => services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/home"));
        var main = provider.GetRequiredService<IRouteableMenuViewModel>();

        Assert.True(main.Start());

        // The failed try gave the start back: the region that appears next time is allowed to try again.
        await UntilAsync(() => main.Start());
        Assert.Null(provider.GetRequiredService<IRouter>().CurrentView);
    }

    [Fact]
    public async Task A_root_whose_view_model_throws_can_be_started_again_too()
    {
        using var provider = Build(services => services.RegisterRoute<FakeViewFor, ThrowingVm>(route: "/"));
        var main = provider.GetRequiredService<IRouteableMenuViewModel>();

        Assert.True(main.Start());

        await UntilAsync(() => main.Start());
        Assert.Null(provider.GetRequiredService<IRouter>().CurrentViewModel);
    }
}
