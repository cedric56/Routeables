using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Routeables.Wpf;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Keyboard = System.Windows.Input.Keyboard;
using Window = System.Windows.Window;

namespace Routeables.Tests.Wpf;

/// <summary>What the dialog and notification tests do with windows: show an owner, find what was opened, press its buttons, clean up.</summary>
internal static class WpfTestWindows
{
    // The window the dialogs and notifications of Routeables are shown over: attached the way Window.AttachRouteables does.
    public static async Task<Window> ShowOwnerAsync(bool visible = true, double left = 120, double top = 80)
    {
        return await WpfHarness.Run(() =>
        {
            var owner = new Window
            {
                Title = "owner",
                Width = 800,
                Height = 600,
                Left = left,
                Top = top,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            if (visible)
                owner.Show();

            RouteableWindows.Add(owner);
            RouteableWindows.Activated(owner);
            return owner;
        });
    }

    // Closes what the test opened, whatever happened, so the next test starts from an application without windows.
    public static async Task CleanUpAsync(Window? owner)
    {
        await WpfHarness.Run(() =>
        {
            if (owner is not null)
                RouteableWindows.Remove(owner);

            // The last opened first: a dialog is owned by the one before it.
            foreach (var window in Application.Current.Windows.OfType<Window>().Reverse().ToList())
            {
                try { window.Close(); }
                catch (InvalidOperationException) { /* already closing */ }
            }
        });
        await WpfHarness.Idle();
    }

    // A task that is started on the thread of the application and not awaited there: a dialog completes when the test presses a button.
    public static async Task<Task<T>> StartAsync<T>(Func<Task<T>> start)
    {
        Task<T>? pending = null;
        await WpfHarness.Run(() => { pending = start(); });
        return pending!;
    }

    public static async Task<Window> WaitForWindowAsync(string title, CancellationToken ct)
    {
        await WpfHarness.WaitUntil(() => Find(title) is not null, $"a window titled '{title}' is shown", ct);
        return (await WpfHarness.Run(() => Find(title)))!;
    }

    public static Window? Find(string title)
        => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.Title == title);

    public static Task<bool> IsOpenAsync(string title) => WpfHarness.Run(() => Find(title) is not null);

    public static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T found)
                yield return found;

            foreach (var inner in Logical<T>(child))
                yield return inner;
        }
    }

    public static Task<string[]> ButtonsAsync(Window dialog)
        => WpfHarness.Run(() => Logical<Button>(dialog).Select(b => b.Content as string ?? string.Empty).ToArray());

    public static Task<string[]> TextsAsync(Window dialog)
        => WpfHarness.Run(() => Logical<TextBlock>(dialog).Select(t => t.Text).ToArray());

    public static Task<bool> IsEnabledAsync(Window dialog, string button)
        => WpfHarness.Run(() => Logical<Button>(dialog).Single(b => (b.Content as string) == button).IsEnabled);

    public static Task<bool> IsDefaultAsync(Window dialog, string button)
        => WpfHarness.Run(() => Logical<Button>(dialog).Single(b => (b.Content as string) == button).IsDefault);

    public static Task ClickAsync(Window dialog, string button)
        => WpfHarness.Run(() => Logical<Button>(dialog).Single(b => (b.Content as string) == button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

    public static Task PressEscapeAsync(Window dialog)
        => WpfHarness.Run(() => dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog)!, 0, Key.Escape)
        {
            RoutedEvent = UIElement.PreviewKeyDownEvent
        }));

    public static Task CloseAsync(Window window) => WpfHarness.Run(() => window.Close());
}
