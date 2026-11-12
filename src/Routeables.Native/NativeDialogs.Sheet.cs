namespace Routeables.Native;

public static partial class NativeDialogs
{
    /// <summary>
    /// Displays a platform-native sheet of choices and awaits the user's selection: an Android alert dialog with a list, a Windows App SDK content dialog with a column of
    /// buttons, an iOS / Mac Catalyst action sheet. It is for the choices a dialog of up to three buttons (<see cref="DisplayThreeButtonsAsync{T}"/>) cannot hold. Call it on the
    /// thread of the interface.
    /// </summary>
    /// <remarks>On the plain <c>net10.0</c> target, and on Android when no activity is known, nothing is shown and <see cref="NativeDialogResult{T}.Shown"/> is <see langword="false"/>: the caller draws its own.</remarks>
    /// <param name="title">The title of the sheet.</param>
    /// <param name="message">The message under the title (the list of Android has no room for it: it is added to the title there).</param>
    /// <param name="choices">The captions of the choices, in order.</param>
    /// <param name="cancel">The caption of the way out.</param>
    /// <param name="owner">Windows only: the <c>Microsoft.UI.Xaml.XamlRoot</c> of the window that shows the sheet (required there, ignored elsewhere).</param>
    /// <param name="dark">Windows only: <see langword="true"/> for the dark theme, <see langword="false"/> for the light one, <see langword="null"/> for the theme of the application.</param>
    /// <returns>The index of the choice made, or <paramref name="choices"/>'s count when the user took the way out or closed the sheet.</returns>
    /// <exception cref="InvalidOperationException">There is no window (Apple) or <paramref name="owner"/> (Windows) to show the sheet on.</exception>
    public static async Task<NativeDialogResult<int>> DisplaySheetAsync(
        string? title, string? message, IReadOnlyList<string> choices, string cancel, object? owner = null, bool? dark = null)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(cancel);

        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = choices.Count;

#if ANDROID
        if (SystemNotificationCenter.CurrentActivity is not { } activity)
            return new(false, default);

        // A list has no message: it goes under the title.
        var heading = string.IsNullOrEmpty(message) ? title : string.IsNullOrEmpty(title) ? message : title + "\n" + message;
        var builder = new Android.App.AlertDialog.Builder(activity);
        builder.SetTitle(heading);
        builder.SetItems([.. choices], (_, e) => source.TrySetResult(e.Which));
        builder.SetNegativeButton(cancel, (_, _) => source.TrySetResult(cancelled));
        var dialog = builder.Create();
        if (dialog is not null)
            dialog.CancelEvent += (_, _) => source.TrySetResult(cancelled);
        dialog?.Show();
        return new(true, await source.Task);
#elif WINDOWS
        var root = owner as Microsoft.UI.Xaml.XamlRoot
            ?? throw new InvalidOperationException("No XamlRoot to host the sheet: give the root of the window.");

        var column = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 8, MinWidth = 320 };
        if (!string.IsNullOrEmpty(message))
            column.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8) });

        var contentDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            RequestedTheme = dark switch
            {
                true => Microsoft.UI.Xaml.ElementTheme.Dark,
                false => Microsoft.UI.Xaml.ElementTheme.Light,
                _ => Microsoft.UI.Xaml.ElementTheme.Default
            },
            Title = title,
            Content = column,
            CloseButtonText = cancel,
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
            XamlRoot = root
        };

        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            var button = new Microsoft.UI.Xaml.Controls.Button
            {
                Content = choices[i],
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
            };
            button.Click += (_, _) =>
            {
                source.TrySetResult(index);
                contentDialog.Hide();
            };
            column.Children.Add(button);
        }

        // Escape, the close button or a click outside is the way out (a choice has already set its own result).
        contentDialog.Closed += (_, _) => source.TrySetResult(cancelled);
        await WindowsDialogGate.WaitAsync();
        try { await contentDialog.ShowAsync(); }
        finally
        {
            WindowsDialogGate.Release();
            column.Children.Clear();
            contentDialog.Content = null;
        }

        return new(true, await source.Task);
#elif MACCATALYST || IOS
        var sheet = UIKit.UIAlertController.Create(title, message, UIKit.UIAlertControllerStyle.ActionSheet);
        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            sheet.AddAction(UIKit.UIAlertAction.Create(choices[i], UIKit.UIAlertActionStyle.Default, _ => source.TrySetResult(index)));
        }

        sheet.AddAction(UIKit.UIAlertAction.Create(cancel, UIKit.UIAlertActionStyle.Cancel, _ => source.TrySetResult(cancelled)));

        var top = KeyWindowController()
            ?? throw new InvalidOperationException("No window to present the sheet; the caller would wait for ever.");
        while (top.PresentedViewController is { } presented)
            top = presented;

        // On an iPad and on Mac Catalyst the sheet is a popover, which must have something to hang from or the system throws.
        if (sheet.PopoverPresentationController is { } popover && top.View is { } view)
        {
            popover.SourceView = view;
            popover.SourceRect = new CoreGraphics.CGRect(view.Bounds.Width / 2, view.Bounds.Height / 2, 1, 1);
            popover.PermittedArrowDirections = 0;
        }

        top.PresentViewController(sheet, true, null);
        return new(true, await source.Task);
#else
        // No native sheet on the plain net10.0 target: the caller draws its own.
        _ = (title, message, owner, dark, source, cancelled);
        await Task.CompletedTask;
        return new(false, default);
#endif
    }
}
