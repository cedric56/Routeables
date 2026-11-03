using System.Windows;

namespace Routeables.Wpf;

// The windows that were attached to Routeables, and the one the user works in: dialogs and notifications are shown over it.
internal static class RouteableWindows
{
    private static readonly List<WeakReference<Window>> _windows = [];
    private static WeakReference<Window>? _active;

    public static void Add(Window window)
    {
        lock (_windows)
        {
            if (!_windows.Any(w => w.TryGetTarget(out var known) && ReferenceEquals(known, window)))
                _windows.Add(new WeakReference<Window>(window));
        }
    }

    public static void Activated(Window window)
    {
        lock (_windows)
            _active = new WeakReference<Window>(window);
    }

    public static void Remove(Window window)
    {
        lock (_windows)
        {
            _windows.RemoveAll(w => !w.TryGetTarget(out var known) || ReferenceEquals(known, window));
            if (_active is not null && (!_active.TryGetTarget(out var active) || ReferenceEquals(active, window)))
                _active = null;
        }
    }

    public static Window? Active
    {
        get
        {
            lock (_windows)
            {
                if (_active is not null && _active.TryGetTarget(out var active))
                    return active;

                foreach (var reference in _windows)
                    if (reference.TryGetTarget(out var window))
                        return window;

                return null;
            }
        }
    }
}
