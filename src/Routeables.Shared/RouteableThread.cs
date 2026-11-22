#if WPF
using System.Windows.Threading;
#elif AVALONIA
using Avalonia.Threading;
#elif MAUI
#else
using Microsoft.UI.Xaml;
#endif

// Shared source for every host: the Routeables controls apply what the router tells them on their own thread.
#if WPF
namespace Routeables.Wpf;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif MAUI
namespace Routeables.Maui;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

internal static class RouteableThread
{
    // Work queued on the control's thread has no caller to receive its exception: left alone, it ends the application (Avalonia, MAUI, WinUI) or
    // is lost (WPF). It goes to the Routeables error channel instead. Work that runs immediately still throws to its caller, which can undo what it started.
    private static void Deferred(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Routeables.Abstraction.Errors.RouteableErrors.Report(null, "RouteableThread.Post", ex);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> now when the caller is on the thread of <paramref name="owner"/>, else queues it there. The router changes
    /// on the thread that asked for the route change: a control can only be touched from its own.
    /// </summary>
    /// <param name="owner">The control.</param>
    /// <param name="action">What to do with it.</param>
#if WPF
    public static void Post(this DispatcherObject owner, Action action)
    {
        if (owner.Dispatcher.CheckAccess())
            action();
        else
            owner.Dispatcher.InvokeAsync(() => Deferred(action));
    }
#elif AVALONIA
    public static void Post(this object owner, Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(() => Deferred(action));
    }
#elif MAUI
    public static void Post(this BindableObject owner, Action action)
    {
        if (owner.Dispatcher.IsDispatchRequired)
            owner.Dispatcher.Dispatch(() => Deferred(action));
        else
            action();
    }
#else
    public static void Post(this DependencyObject owner, Action action)
    {
        if (owner.DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => Deferred(action));
        else
            action();
    }
#endif
}
