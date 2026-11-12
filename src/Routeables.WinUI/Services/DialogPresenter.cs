using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Services;

#if WINUI3
namespace Routeables.WinUI3.Services;
#else
namespace Routeables.Uno.Services;
#endif

internal static class DialogPresenter
{
    // A window shows one ContentDialog at a time: the next one waits for the one on screen.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static void UseDialogButtons(ContentDialog dialog, IReadOnlyList<DialogAction> actions)
    {
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            switch (i)
            {
                case 0:
                    dialog.PrimaryButtonText = action.Text;
                    dialog.IsPrimaryButtonEnabled = action.IsEnabled;
                    dialog.PrimaryButtonClick += (_, e) => { e.Cancel = true; action.Invoke(); };
                    action.Changed += () => dialog.IsPrimaryButtonEnabled = action.IsEnabled;
                    break;
                case 1:
                    dialog.SecondaryButtonText = action.Text;
                    dialog.IsSecondaryButtonEnabled = action.IsEnabled;
                    dialog.SecondaryButtonClick += (_, e) => { e.Cancel = true; action.Invoke(); };
                    action.Changed += () => dialog.IsSecondaryButtonEnabled = action.IsEnabled;
                    break;
                default:
                    dialog.CloseButtonText = action.Text;
                    dialog.CloseButtonClick += (_, e) => { e.Cancel = true; action.Invoke(); };
                    break;
            }

            if (action.IsDefault)
                dialog.DefaultButton = i switch { 0 => ContentDialogButton.Primary, 1 => ContentDialogButton.Secondary, _ => ContentDialogButton.Close };
        }
    }

    /// <summary>Shows <paramref name="card"/> in a modal dialog and returns once it is closed.</summary>
    public static async Task ShowAsync(XamlRoot root, string? title, DialogCardContent card, RouteableDialogHandle handle, bool fullSize = false)
    {
        await Gate.WaitAsync();
        try
        {
            // The card carries its own buttons: the dialog has none.
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = title,
                Content = card.Body,
                DefaultButton = ContentDialogButton.None,
            };

            // Every dialog has the buttons of the ContentDialog, so that they all look alike (and like the three-button dialog of Routeables.Native).
            // Pressing one does not close it: the code decides when (handle.Complete), as before.
            if (card.Actions.Count <= 3)
                UseDialogButtons(dialog, card.Actions);
            else
                DialogCard.AddButtonSheet(card.Body, card.Actions);

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
                // On Windows the dialogs of the platform (Routeables.Native) wait for this one, and this one for them: WinUI throws for a second ContentDialog.
                await Routeables.Native.NativeDialogs.OneAtATimeAsync(async () =>
                {
                    await dialog.ShowAsync();
                    return 0;
                });
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
