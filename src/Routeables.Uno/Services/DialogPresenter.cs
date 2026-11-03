using Routeables.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Routeables.Uno.Services;

internal static class DialogPresenter
{
    // A window shows one ContentDialog at a time: the next one waits for the one on screen.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Shows <paramref name="body"/> in a modal dialog and returns once it is closed.</summary>
    public static async Task ShowAsync(XamlRoot root, string? title, UIElement body, RouteableDialogHandle handle, bool fullSize = false)
    {
        await Gate.WaitAsync();
        try
        {
            // The card carries its own buttons: the dialog has none.
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = title,
                Content = body,
                DefaultButton = ContentDialogButton.None,
            };

            if (fullSize)
            {
                dialog.Resources["ContentDialogMinWidth"] = 640.0;
                dialog.Resources["ContentDialogMaxWidth"] = 1200.0;
            }

            handle.CloseSurface = dialog.Hide;
            dialog.Opened += (_, _) => handle.NotifyOpened();
            dialog.Closing += (_, e) =>
            {
                // Escape: the code decides whether the user may leave.
                if (!handle.AcceptDismiss())
                    e.Cancel = true;
            };

            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                handle.CloseSurface = null;
                dialog.Content = null;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
