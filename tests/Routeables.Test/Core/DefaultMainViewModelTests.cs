using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>An application without a menu: <see cref="MainViewModelDefault"/> and <c>AddHome</c>.</summary>
public class DefaultMainViewModelTests
{
    private class DefaultMainViewModel(IRouter router) : MainViewModelDefault(router, "/home") { 
    }

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;

    private static ServiceProvider Build(string? home)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterMainViewModel<DefaultMainViewModel>(ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
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

    [Fact]
    public void The_default_main_view_model_has_no_entry()
    {
        using var provider = Build(null);

        Assert.Empty(provider.GetRequiredService<IMainViewModel>().Routeables);
    }

    [Fact]
    public async Task The_home_route_is_opened_when_the_host_selects_the_first_entry()
    {
        using var provider = Build("/home");
        var main = provider.GetRequiredService<IMainViewModel>();

        Assert.True(main.SelectFirst());

        await UntilAsync(() => provider.GetRequiredService<IRouter>().CurrentViewModel is HomeVm);
        await UntilAsync(() => main.CurrentView is HomeView);
    }
}
