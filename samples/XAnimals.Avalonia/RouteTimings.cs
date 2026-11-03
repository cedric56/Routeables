using System;
using System.Diagnostics;
using Avalonia.Threading;
using Routeables.Abstraction;

namespace XAnimals.Avalonia;

/// <summary>
/// Measures a route change on the device and writes it to the console (logcat on Android, tag DOTNET), one line per change:
/// <c>[timing] Cats Push  router 3.1 ms  ready 142.8 ms</c>.
/// <list type="bullet">
/// <item><description><b>router</b>: from the start of the route change to its end (guards, view and view model creation, initialization). The view is created and bound here.</description></item>
/// <item><description><b>ready</b>: from the same start to the moment the UI thread has nothing left to do for it (layout, measure and render of the new view are done): what the user waits for, apart from the touch.</description></item>
/// </list>
/// A big <b>ready</b> with a small <b>router</b> is the cost of the view (XAML, template, images), not of Routeables.
/// </summary>
internal static class RouteTimings
{
    public static void Attach(IRouter router)
    {
        var started = 0L;

        router.Routing += (_, _) => started = Stopwatch.GetTimestamp();

        router.Routed += (_, e) =>
        {
            var begin = started;
            var routerTime = Stopwatch.GetElapsedTime(begin);
            var name = e.To.ViewModel.GetType().Name;

            // Background is below layout and render: the callback runs once the new view is on screen.
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var ready = Stopwatch.GetElapsedTime(begin);
                Console.WriteLine($"[timing] {name} {e.Kind} {e.Outcome}  router {routerTime.TotalMilliseconds:0.0} ms  ready {ready.TotalMilliseconds:0.0} ms");
            }, DispatcherPriority.Background);
        };
    }
}
