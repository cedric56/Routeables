using Microsoft.Maui.Platform;
using Routeables.Abstraction;
using Routeables.Routing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Routeables.Test.Maui;

/// <summary>
/// What the tests share: the container of the running application, its routers, and a way to give a control a real handler (a native control)
/// without a page of its own. The routers are those of the application, so every test starts by emptying them (<see cref="ResetAsync"/>).
/// </summary>
internal static class Device
{
    public static IServiceProvider Services => IPlatformApplication.Current!.Services;

    public static IRouter Router(string? region = null) => Services.GetRouter(region);

    public static async Task ResetAsync()
    {
        await Router().EraseHistoryAsync();
        await Router(MauiProgram.Side).EraseHistoryAsync();
    }

    /// <summary>The MAUI context of the window of the runner: what a handler needs to build a native control.</summary>
    private static IMauiContext Context
        => IPlatformApplication.Current!.Application!.Windows[0].Handler!.MauiContext!;

    /// <summary>Gives <paramref name="view"/> its handler, the native control: a <c>RouteableRegion</c> listens to its router from then on.</summary>
    public static T Attach<T>(T view) where T : View
    {
        _ = view.ToPlatform(Context);
        return view;
    }

    /// <summary>Takes the handler away, as when the control leaves the screen.</summary>
    public static void Detach(View view)
    {
        view.Handler?.DisconnectHandler();
        view.Handler = null;
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

    /// <summary>The control as it is when nothing is playing on it.</summary>
    public static bool IsAtRest(View view)
        => view.Opacity == 1 && view.Scale == 1 && view.ScaleX == 1 && view.ScaleY == 1 && view.Rotation == 0
           && view.TranslationX == 0 && view.TranslationY == 0;

    public static IRouteableArgs With(RouteTransition transition) => RouteableArgs.Create().WithTransition(transition);
}

/// <summary>A control with a handler for the time of a test; the handler goes with it.</summary>
internal sealed class Hosted<T> : IDisposable where T : View
{
    public Hosted(T view) => View = Device.Attach(view);

    public T View { get; }

    public void Dispose() => Device.Detach(View);
}
