namespace Routeables.Native;

public static partial class NativeDialogs
{
    /// <summary>
    /// Displays a platform-native dialog with up to three buttons and awaits the user's selection: an Android alert dialog, a Windows App SDK
    /// content dialog, or an iOS / Mac Catalyst alert controller (a text) or form sheet (a <c>UIView</c>). Call it on the thread of the interface.
    /// </summary>
    /// <remarks>
    /// The content the platform can show: a <see cref="string"/> everywhere; an <c>Android.Views.View</c> on Android; a <c>UIKit.UIView</c> on iOS and
    /// Mac Catalyst; any <c>UIElement</c> on Windows. For another content, on the plain <c>net10.0</c> target, and on Android when no activity is known
    /// (<see cref="Initialize"/> came after the first activity), nothing is shown and
    /// <see cref="NativeDialogResult{T}.Shown"/> is <see langword="false"/>: the caller draws its own dialog.
    /// </remarks>
    /// <typeparam name="T">The result type associated with each button.</typeparam>
    /// <param name="title">The dialog title.</param>
    /// <param name="content">The dialog content: a message string, or a platform view to embed.</param>
    /// <param name="primaryButtonText">The primary button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="secondaryButtonText">The secondary button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="closeButtonText">The close button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="primaryResult">The result returned if the primary button is selected.</param>
    /// <param name="secondaryResult">The result returned if the secondary button is selected.</param>
    /// <param name="closeResult">The result returned if the close button is selected, or if the dialog is dismissed without a button.</param>
    /// <param name="owner">Windows only: the <c>Microsoft.UI.Xaml.XamlRoot</c> of the window that shows the dialog (required there, ignored elsewhere).</param>
    /// <param name="dark">Windows only: <see langword="true"/> for the dark theme, <see langword="false"/> for the light one, <see langword="null"/> for the theme of the application.</param>
    /// <exception cref="InvalidOperationException">There is no window (Apple) or <paramref name="owner"/> (Windows) to show the dialog on.</exception>
    public static async Task<NativeDialogResult<T>> DisplayThreeButtonsAsync<T>(
        string? title, object? content,
        string? primaryButtonText, string? secondaryButtonText, string? closeButtonText,
        T primaryResult, T secondaryResult, T closeResult,
        object? owner = null, bool? dark = null)
    {
        var taskCompletionSource = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);

#if ANDROID
        if (content is not (string or Android.Views.View))
            return new(false, default);

        // No activity (the application is in the background or starting, or the host was started after the first activity): the caller draws its own.
        if (SystemNotificationCenter.CurrentActivity is not { } activity)
            return new(false, default);

        var alertBuilder = new Android.App.AlertDialog.Builder(activity);

        alertBuilder.SetTitle(title);
        if (content is string message)
            alertBuilder.SetMessage(message);
        else
            alertBuilder.SetView((Android.Views.View)content);

        if (!string.IsNullOrEmpty(primaryButtonText))
            alertBuilder.SetPositiveButton(primaryButtonText, (_, _) => taskCompletionSource.TrySetResult(primaryResult));
        if (!string.IsNullOrEmpty(secondaryButtonText))
            alertBuilder.SetNegativeButton(secondaryButtonText, (_, _) => taskCompletionSource.TrySetResult(secondaryResult));
        if (!string.IsNullOrEmpty(closeButtonText))
            alertBuilder.SetNeutralButton(closeButtonText, (_, _) => taskCompletionSource.TrySetResult(closeResult));

        var dialog = alertBuilder.Create();
        // Tapping outside the dialog or pressing Back closes it without a button.
        if (dialog is not null)
            dialog.CancelEvent += (_, _) => taskCompletionSource.TrySetResult(closeResult);
        dialog?.Show();
        return new(true, await taskCompletionSource.Task);
#elif WINDOWS
        var root = owner as Microsoft.UI.Xaml.XamlRoot
            ?? throw new InvalidOperationException("No XamlRoot to host the content dialog: give the root of the window.");
        var contentDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            RequestedTheme = dark switch
            {
                true => Microsoft.UI.Xaml.ElementTheme.Dark,
                false => Microsoft.UI.Xaml.ElementTheme.Light,
                _ => Microsoft.UI.Xaml.ElementTheme.Default
            },
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = string.IsNullOrEmpty(primaryButtonText) ? Microsoft.UI.Xaml.Controls.ContentDialogButton.None : Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
            XamlRoot = root
        };
        // Escape or a click outside closes it without a button: that is the close result, like a cancel on Android (a button has already set its own).
        contentDialog.Closed += (_, _) => taskCompletionSource.TrySetResult(closeResult);
        contentDialog.PrimaryButtonClick += (_, _) => taskCompletionSource.TrySetResult(primaryResult);
        contentDialog.SecondaryButtonClick += (_, _) => taskCompletionSource.TrySetResult(secondaryResult);
        contentDialog.CloseButtonClick += (_, _) => taskCompletionSource.TrySetResult(closeResult);
        // WinUI allows one ContentDialog at a time per window: a second ShowAsync throws. Wait for the one that is open.
        await WindowsDialogGate.WaitAsync();
        try { await contentDialog.ShowAsync(); }
        finally { WindowsDialogGate.Release(); }
        return new(true, await taskCompletionSource.Task);
#elif MACCATALYST || IOS
        if (content is string text)
        {
            var alert = new UIKit.UIAlertController { Title = title, Message = text };
            if (!string.IsNullOrEmpty(primaryButtonText))
                alert.AddAction(UIKit.UIAlertAction.Create(primaryButtonText, UIKit.UIAlertActionStyle.Default, _ => taskCompletionSource.TrySetResult(primaryResult)));
            if (!string.IsNullOrEmpty(secondaryButtonText))
                alert.AddAction(UIKit.UIAlertAction.Create(secondaryButtonText, UIKit.UIAlertActionStyle.Default, _ => taskCompletionSource.TrySetResult(secondaryResult)));
            if (!string.IsNullOrEmpty(closeButtonText))
                alert.AddAction(UIKit.UIAlertAction.Create(closeButtonText, UIKit.UIAlertActionStyle.Default, _ => taskCompletionSource.TrySetResult(closeResult)));
            var rootViewController = KeyWindowController()
                ?? throw new InvalidOperationException("No window to present the alert; the caller would wait for ever.");
            // An alert with no button cannot be dismissed: the caller would wait for ever. It gets one that gives the close result.
            if (alert.Actions.Length == 0)
                alert.AddAction(UIKit.UIAlertAction.Create("OK", UIKit.UIAlertActionStyle.Default, _ => taskCompletionSource.TrySetResult(closeResult)));
            rootViewController.PresentViewController(alert, true, null);
            return new(true, await taskCompletionSource.Task);
        }

        if (content is UIKit.UIView contentView)
        {
            var vc = new UIKit.UIViewController { ModalPresentationStyle = UIKit.UIModalPresentationStyle.FormSheet };
            vc.PreferredContentSize = new CoreGraphics.CGSize(320, 360);

            var stack = new UIKit.UIStackView { Axis = UIKit.UILayoutConstraintAxis.Vertical, Spacing = 12 };
            stack.TranslatesAutoresizingMaskIntoConstraints = false;

            if (!string.IsNullOrEmpty(title))
            {
                var label = new UIKit.UILabel { Text = title };
                if (UIKit.UIFont.BoldSystemFontOfSize(17) is { } font)
                    label.Font = font;
                else if (UIKit.UIFont.SystemFontOfSize(17) is { } font2)
                    label.Font = font2;
                stack.AddArrangedSubview(label);
            }
            contentView.TranslatesAutoresizingMaskIntoConstraints = false;
            stack.AddArrangedSubview(contentView);

            var buttons = new UIKit.UIStackView
            {
                Axis = UIKit.UILayoutConstraintAxis.Horizontal,
                Spacing = 8,
                Distribution = UIKit.UIStackViewDistribution.FillEqually
            };

            void AddButton(string? buttonText, T result)
            {
                if (string.IsNullOrEmpty(buttonText)) return;
                var button = UIKit.UIButton.FromType(UIKit.UIButtonType.System);
                button.SetTitle(buttonText, UIKit.UIControlState.Normal);
                button.TouchUpInside += (_, _) => { taskCompletionSource.TrySetResult(result); vc.DismissViewController(true, null); };
                buttons.AddArrangedSubview(button);
            }
            AddButton(primaryButtonText, primaryResult);
            AddButton(secondaryButtonText, secondaryResult);
            AddButton(closeButtonText, closeResult);
            stack.AddArrangedSubview(buttons);

            vc.View!.AddSubview(stack);
            UIKit.NSLayoutConstraint.ActivateConstraints(
            [
                stack.LeadingAnchor.ConstraintEqualTo(vc.View.LeadingAnchor, 16),
                stack.TrailingAnchor.ConstraintEqualTo(vc.View.TrailingAnchor, -16),
                stack.TopAnchor.ConstraintEqualTo(vc.View.TopAnchor, 16),
                stack.BottomAnchor.ConstraintEqualTo(vc.View.BottomAnchor, -16),
            ]);

            var top = KeyWindowController();
            while (top?.PresentedViewController is { } presented)
                top = presented;

            // The sheet was built but never presented, so the caller waited for ever.
            if (top is null)
                return new(true, closeResult);

            // Only the buttons close it: a swipe down would dismiss it without a result and leave the caller waiting.
            vc.ModalInPresentation = true;
            top.PresentViewController(vc, true, null);
            return new(true, await taskCompletionSource.Task);
        }

        return new(false, default);
#else
        // No native dialog on the plain net10.0 target: the caller draws its own.
        _ = (title, content, primaryButtonText, secondaryButtonText, closeButtonText, primaryResult, secondaryResult, closeResult, owner, dark, taskCompletionSource);
        await Task.CompletedTask;
        return new(false, default);
#endif
    }

#if MACCATALYST || IOS
    private static UIKit.UIViewController? KeyWindowController()
        => UIKit.UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIKit.UIWindowScene>()
            .SelectMany(s => s.Windows)
            .FirstOrDefault(w => w.IsKeyWindow)?.RootViewController;
#endif
}
