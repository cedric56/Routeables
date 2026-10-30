using Routeables.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

/// <summary>
/// Shows a dialog card modally over a <see cref="TopLevel"/> with plain Avalonia controls: in an owned window on
/// desktop, in the top level's overlay layer where there is only one view (Android, iOS, Browser).
/// </summary>
internal static class DialogPresenter
{
    // Dialogs currently open, oldest first. Only touched on the UI thread.
    private static readonly List<Window> OpenWindows = [];
    private static readonly List<Border> OpenBackdrops = [];

    /// <summary>Shows <paramref name="card"/> and completes when the dialog is closed.</summary>
    public static Task ShowAsync(TopLevel owner, string? icon, string? title, Control card, RouteableDialogHandle handle)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(handle);

        return owner is Window window
            ? ShowInWindowAsync(window, icon, title, card, handle)
            : ShowInOverlayAsync(owner, title, card, handle);
    }

    private static async Task ShowInWindowAsync(Window owner, string? icon, string? title, Control card, RouteableDialogHandle handle)
    {
        // The window icon is built from the bitmap; the bitmap and its stream are released when the dialog is closed, or
        // each dialog would keep a native image for the life of the application.
        using var stream = string.IsNullOrEmpty(icon) ? null : AssetLoader.Open(new Uri(icon));
        using var bitmap = stream is null ? null : new Bitmap(stream);

        // A window that already has a modal child cannot be the owner of another modal: a dialog opened while
        // others are open is owned by the topmost one, so it appears over it and the ones below stay blocked.
        var parent = OpenWindows.LastOrDefault(open => IsOwnedBy(open, owner)) ?? owner;
        var dialog = new Window
        {
            Icon = bitmap is null ? owner.Icon : new WindowIcon(bitmap),   // no icon given: the one of the window that opens the dialog
            Title = title ?? string.Empty,
            Content = card,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            MinWidth = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        // SizeToContent is applied after the window is shown, so the window would first appear in a corner and then jump to the centre.
        var offscreen = parent.Bounds.Width > 0 && parent.Bounds.Height > 0;
        if (offscreen)
            PlaceOffscreen(dialog);

        handle.CloseSurface = dialog.Close;

        // The window's own close button and Escape are dismissals that the dialog may veto.
        dialog.Closing += (_, e) =>
        {
            if (!handle.AcceptDismiss())
                e.Cancel = true;
        };
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                handle.RequestDismiss();
            }
        };
        dialog.Opened += (_, _) =>
        {
            handle.NotifyOpened();
            if (offscreen)
                Dispatcher.UIThread.Post(() => CenterOnOwner(dialog, parent), DispatcherPriority.Loaded);
        };

        OpenWindows.Add(dialog);
        try
        {
            await dialog.ShowDialog(parent);
        }
        finally
        {
            OpenWindows.Remove(dialog);
        }
    }

    // The window sizes itself to its content (as it always did), but only after it is shown, and an estimate of the size is wrong for
    // a content that is not in the tree yet (a view with a template). So the window opens far outside the screen, where nobody sees
    // it, and is centred on its owner once its real size is known.
    private static void PlaceOffscreen(Window dialog)
    {
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Position = new PixelPoint(-32000, -32000);
    }

    private static void CenterOnOwner(Window dialog, Window parent)
    {
        var scale = dialog.RenderScaling;
        var width = (int)Math.Round(dialog.Bounds.Width * scale);
        var height = (int)Math.Round(dialog.Bounds.Height * scale);
        var parentWidth = (int)Math.Round(parent.Bounds.Width * parent.RenderScaling);
        var parentHeight = (int)Math.Round(parent.Bounds.Height * parent.RenderScaling);
        if (width <= 0 || height <= 0 || parentWidth <= 0 || parentHeight <= 0)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }

        dialog.Position = new PixelPoint(
            parent.Position.X + (parentWidth - width) / 2,
            parent.Position.Y + (parentHeight - height) / 2);
    }

    // Whether `window` is `root` or is owned, directly or not, by it.
    private static bool IsOwnedBy(Window window, Window root)
    {
        WindowBase? current = window;
        while (current is not null)
        {
            if (ReferenceEquals(current, root))
                return true;

            current = (current as Window)?.Owner;
        }

        return false;
    }

    private static Task ShowInOverlayAsync(TopLevel owner, string? title, Control card, RouteableDialogHandle handle)
    {
        var layer = OverlayLayer.GetOverlayLayer(owner)
            ?? throw new InvalidOperationException(
                "The top level has no overlay layer yet, so the dialog cannot be displayed. Show the dialog once the main view is loaded.");

        var dark = Equals(owner.ActualThemeVariant, ThemeVariant.Dark);
        var surface = new Border
        {
            Background = new SolidColorBrush(dark ? Color.Parse("#2B2B2B") : Colors.White),
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16),
            Child = WithTitle(title, card, dark)
        };

        var backdrop = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            Focusable = true,
            Child = surface
        };
        Canvas.SetLeft(backdrop, 0);
        Canvas.SetTop(backdrop, 0);

        // The overlay layer is a canvas: its children are not stretched, so the backdrop follows the layer's size.
        void Resize()
        {
            backdrop.Width = layer.Bounds.Width;
            backdrop.Height = layer.Bounds.Height;
        }

        void OnLayerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty)
                Resize();
        }

        var closed = new TaskCompletionSource();

        handle.CloseSurface = () =>
        {
            layer.PropertyChanged -= OnLayerChanged;
            layer.Children.Remove(backdrop);
            OpenBackdrops.Remove(backdrop);
            closed.TrySetResult();

            // The dialog underneath becomes the active one again: it takes the keyboard (Escape) back.
            var below = OpenBackdrops.LastOrDefault(open => ReferenceEquals(open.Parent, layer));
            if (below is not null)
                Dispatcher.UIThread.Post(() => below.Focus());
        };

        backdrop.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                handle.RequestDismiss();
            }
        };

        layer.PropertyChanged += OnLayerChanged;
        Resize();
        layer.Children.Add(backdrop);
        OpenBackdrops.Add(backdrop);

        // The owner can go away with the dialog still open (its window is closed): nobody calls CloseSurface then, and the
        // static list would keep the backdrop (and through it the whole window) for the life of the application.
        backdrop.DetachedFromVisualTree += (_, _) =>
        {
            layer.PropertyChanged -= OnLayerChanged;
            OpenBackdrops.Remove(backdrop);

            // Nobody will close the dialog any more: let the caller's await finish (with the default result) instead of
            // leaving it, and everything it holds, waiting for a click that cannot happen.
            closed.TrySetResult();
        };

        Dispatcher.UIThread.Post(() =>
        {
            backdrop.Focus();
            handle.NotifyOpened();
        });

        return closed.Task;
    }

    // A window has a title bar; the overlay does not, so the title goes inside the card.
    private static Control WithTitle(string? title, Control card, bool dark)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Foreground(card, dark);

        var panel = new StackPanel { Spacing = 0, MaxWidth = 560 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(20, 20, 20, 0),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(card);
        return Foreground(panel, dark);
    }

    private static Control Foreground(Control control, bool dark)
    {
        var wrapper = new ContentControl
        {
            Foreground = new SolidColorBrush(dark ? Colors.White : Colors.Black),
            Content = control
        };
        return wrapper;
    }
}