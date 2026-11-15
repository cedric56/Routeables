#if WPF
using System.Windows.Threading;
#elif AVALONIA
using Avalonia.Threading;
#elif MAUI
#else
using Microsoft.UI.Xaml;
#endif

// One file for every host: the controls of Routeables put what the router tells them on their own thread.
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
            owner.Dispatcher.InvokeAsync(action);
    }
#elif AVALONIA
    public static void Post(this object owner, Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
#elif MAUI
    public static void Post(this BindableObject owner, Action action)
    {
        if (owner.Dispatcher.IsDispatchRequired)
            owner.Dispatcher.Dispatch(action);
        else
            action();
    }
#else
    public static void Post(this DependencyObject owner, Action action)
    {
        if (owner.DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => action());
        else
            action();
    }
#endif
}
