using System.Windows;
using System.Windows.Threading;

namespace Routeables.Wpf;

// Dialogs and notifications may be requested from any thread: they are always shown by the application's dispatcher.
internal static class UiThread
{
    public static Dispatcher Dispatcher =>
        Application.Current?.Dispatcher
        ?? throw new InvalidOperationException("There is no WPF application: dialogs and notifications need one to be shown.");

    public static Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = Dispatcher;
        return dispatcher.CheckAccess() ? action() : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    public static void Post(Action action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
