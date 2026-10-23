using Microsoft.UI.Xaml;

namespace Routeables.Uno;

/// <summary>
/// The windows that were given to <see cref="Extensions.AttachRouteables"/>, and which of them the user is working in.
/// The dialogs and the notifications of <c>Routeables.Uno.Services</c> are shown in the active one, and the theme service
/// changes its content.
/// </summary>
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

    public static void Clear()
    {
        lock (_windows)
        {
            _windows.Clear();
            _active = null;
        }
    }

    /// <summary>The window the user last worked in, or the first one that is still alive.</summary>
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

    /// <summary>The content of <see cref="Active"/> when it is an element.</summary>
    public static FrameworkElement? ActiveRoot => Active?.Content as FrameworkElement;

    /// <summary>What a <c>ContentDialog</c> or a <c>Popup</c> needs to be shown in the active window; <see langword="null"/> before it has content.</summary>
    public static XamlRoot? ActiveXamlRoot => ActiveRoot?.XamlRoot;
}
