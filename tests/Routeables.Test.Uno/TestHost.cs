using Routeables.Abstraction.Routing;
using Routeables.Abstraction.Transitions;
using Routeables.Routing;

namespace Routeables.Test.Uno;

/// <summary>What the tests share: the routers of the running application and the zone of the window where a control is loaded.</summary>
internal static class TestHost
{
    public static IServiceProvider Services => ((App)Application.Current).Services;

    public static IRouter Router(string? region = null) => Services.GetRouter(region);

    public static async Task ResetAsync()
    {
        await Router().EraseHistoryAsync();
        await Router(App.Side).EraseHistoryAsync();
    }

    /// <summary>Puts <paramref name="element"/> in the tree of the window and waits until it is loaded.</summary>
    public static async Task<T> LoadAsync<T>(T element) where T : FrameworkElement
    {
        MainPage.Host.Children.Add(element);
        await UntilAsync(() => element.IsLoaded);
        return element;
    }

    /// <summary>Takes <paramref name="element"/> out of the tree and waits until it is unloaded.</summary>
    public static async Task UnloadAsync(FrameworkElement element)
    {
        MainPage.Host.Children.Remove(element);
        await UntilAsync(() => !element.IsLoaded);
    }

    public static async Task UntilAsync(Func<bool> condition, int seconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"The condition was not met in {seconds} seconds.");
            await Task.Delay(10);
        }
    }

    public static IRouteableArgs With(RouteTransition transition) => RouteableArgs.Create().WithTransition(transition);
}
