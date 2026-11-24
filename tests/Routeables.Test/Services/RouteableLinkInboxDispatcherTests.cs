using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>The inbox in an application that has a UI thread: an address is followed on that thread, whoever pushed it.</summary>
public class RouteableLinkInboxDispatcherTests
{
    // Not on the UI thread whoever asks: what the inbox does with the router has to go through it.
    private sealed class OtherThreadDispatcher : IRouteableDispatcher
    {
        public int Calls;
        public bool CheckAccess() => false;
        public void Post(Action action) => action();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> function) => Task.FromResult(function());
        public Task InvokeAsync(Func<Task> function)
        {
            Interlocked.Increment(ref Calls);
            return function();
        }
    }

    [Fact]
    public async Task An_address_pushed_from_another_thread_is_followed_through_the_dispatcher()
    {
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = new OtherThreadDispatcher();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/di-home");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(route: "/di-link");
        services.AddSingleton<IRouteableDispatcher>(dispatcher);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/di-home", cancellationToken: ct);
        var inbox = new RouteableLinkInbox(provider, ["di"]);

        inbox.Push(new Uri("di://di-link"));

        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < until && router.CurrentViewModel is not SecondViewModel)
            await Task.Delay(20, ct);
        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
        Assert.True(Volatile.Read(ref dispatcher.Calls) >= 1);
    }
}
