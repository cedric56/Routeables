using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>Shows a dialog card modally over a window, in an owned window.</summary>
internal static class DialogPresenter
{
    // Dialogs currently open, oldest first. Only touched on the UI thread.
    private static readonly List<Window> OpenWindows = [];

    /// <summary>Shows <paramref name="card"/> and completes when the dialog is closed.</summary>
    public static Task ShowAsync(Window owner, string? icon, string? title, UIElement card, RouteableDialogHandle handle)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(handle);

        // A window that already has a modal child cannot own another modal: a dialog opened while others are open is
        // owned by the topmost one, so it appears over it and the ones below stay blocked.
        var parent = OpenWindows.LastOrDefault(open => IsOwnedBy(open, owner)) ?? owner;
        var dialog = new Window
        {
            Title = title ?? string.Empty,
            Content = card,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            MinWidth = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = LoadIcon(icon) ?? owner.Icon   // no icon given: the one of the window that opens the dialog
        };

        // An owner that was never shown cannot own a window.
        if (parent.IsVisible)
            dialog.Owner = parent;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var closed = new TaskCompletionSource();
        var opened = false;

        // The window's own close button and Escape are dismissals that the dialog may veto.
        dialog.Closing += (_, e) =>
        {
            if (!handle.AcceptDismiss())
                e.Cancel = true;
        };
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                handle.RequestDismiss();
            }
        };

        // The surface is announced once the dialog is on screen: a dialog completed by code before that is closed by NotifyOpened.
        dialog.ContentRendered += (_, _) =>
        {
            if (opened)
                return;

            opened = true;
            handle.CloseSurface = dialog.Close;
            handle.NotifyOpened();
        };

        dialog.Closed += (_, _) =>
        {
            handle.CloseSurface = null;
            OpenWindows.Remove(dialog);

            // The caller's controls must not keep this window as their parent, or they could not be shown again.
            dialog.Content = null;
            closed.TrySetResult();
        };

        OpenWindows.Add(dialog);

        // ShowDialog runs a nested message loop and returns when the dialog is closed: it is started from a dispatcher
        // callback, so the caller's await is not suspended inside that loop.
        dialog.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                OpenWindows.Remove(dialog);
                closed.TrySetException(ex);
            }
        });

        return closed.Task;
    }

    // Whether `window` is `root` or is owned, directly or not, by it.
    private static bool IsOwnedBy(Window window, Window root)
    {
        Window? current = window;
        while (current is not null)
        {
            if (ReferenceEquals(current, root))
                return true;

            current = current.Owner;
        }

        return false;
    }

    private static ImageSource? LoadIcon(string? icon)
    {
        if (string.IsNullOrEmpty(icon))
            return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(icon, UriKind.RelativeOrAbsolute);
        image.CacheOption = BitmapCacheOption.OnLoad;   // the file or resource stream is not kept open
        image.EndInit();
        image.Freeze();
        return image;
    }
}
