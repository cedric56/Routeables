using System.Runtime.InteropServices;

namespace Routeables.Wpf.Services;

// Windows places a window in the pixels of the screen; WPF gives only units that depend on the monitor the window is on.
internal static class NativeMethods
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    /// <summary>The size of the window, in pixels.</summary>
    public static bool TryGetSize(IntPtr handle, out (int Width, int Height) size)
    {
        if (TryGetBounds(handle, out var bounds))
        {
            size = (bounds.Width, bounds.Height);
            return true;
        }

        size = default;
        return false;
    }

    /// <summary>The position and the size of the window, in pixels of the screen.</summary>
    public static bool TryGetBounds(IntPtr handle, out (int X, int Y, int Width, int Height) bounds)
    {
        if (handle != IntPtr.Zero && GetWindowRect(handle, out var rect))
        {
            bounds = (rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>Moves the window to a position in pixels of the screen, keeping its size and without activating it.</summary>
    public static bool TryMove(IntPtr handle, int x, int y)
        => handle != IntPtr.Zero && SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
}
