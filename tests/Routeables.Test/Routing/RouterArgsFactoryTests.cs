using Routeables.Abstraction;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Routing;

/// <summary>The arguments the pages receive are made by the factory the application registered, whatever the way the route change was made.</summary>
public class RouterArgsFactoryTests
{
    private sealed class OwnArgs : RouteableArgs;

    public sealed class RecordingViewModel : IRouteable
    {
        public static IRouteableArgs? Received { get; set; }

        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            Received = context;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_page_receives_the_arguments_type_of_the_application(bool withArguments)
    {
        var services = new ServiceCollection();
        services.RegisterRouteableArgsFactory(() => new OwnArgs());
        services.AddRoutes(n => n.Page<FakeViewFor, RecordingViewModel>("/page/{id:int}"));
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        RecordingViewModel.Received = null;

        var result = withArguments
            ? await router.GoToAsync("/page/7", RouteableArgs.Create().WithValue("seen", true), cancellationToken: TestContext.Current.CancellationToken)
            : await router.GoToAsync("/page/7", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.IsType<OwnArgs>(RecordingViewModel.Received);
        Assert.Equal("/page/7", RecordingViewModel.Received.Route);
    }
}
