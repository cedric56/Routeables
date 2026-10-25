using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui.Services;

public class RouteableDialogService(IServiceProvider provider, IRouteableDialogWindowContext windowContext) : RouteableDialogServiceBase
{
#if WINDOWS
    private static readonly SemaphoreSlim WindowsDialogGate = new(1, 1);
#endif

    // WinUI shows one dialog at a time per window, and the alert of a Page is one: it waits for the one that is open (the content
    // dialog of DisplayThreeButtonsAsync is behind the same gate). Elsewhere the platform queues or stacks them itself.
    private static async Task<int> OneAtATimeAsync(Func<Task<int>> show)
    {
#if WINDOWS
        await WindowsDialogGate.WaitAsync();
        try { return await show(); }
        finally { WindowsDialogGate.Release(); }
#else
        return await show();
#endif
    }

    /// <summary>
    /// Shows the platform's alert: one button for one word, an alert with an accept and a cancel button for two, and the
    /// native three-button dialog for three. The confirmations (<see cref="IRouteableDialogService"/>) are written once, in
    /// <see cref="RouteableDialogServiceBase"/>.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="buttonText">The buttons written together, one word per capital letter: <c>"YesNoCancel"</c>.</param>
    /// <returns>The index of the button chosen: 0 for the first.</returns>
    public override async Task<int> ShowMessageAsync(string message, string title, string buttonText)
    {
        var page = windowContext.CurrentPage ?? throw new InvalidOperationException(
            "No active Page to host the content dialog popup.");

        var words = RouteableButtons.Split(buttonText);
        switch (words.Count)
        {
            case 0:
            case 1:
                return await OneAtATimeAsync(async () =>
                {
                    await page.DisplayAlertAsync(title, message, words.Count == 0 ? "Ok" : words[0]);
                    return 0;
                });
            case 2:
                return await OneAtATimeAsync(async () => await page.DisplayAlertAsync(title, message, words[0], words[1]) ? 0 : 1);
            default:
                return await DisplayThreeButtonsAsync(title, message, words[0], words[1], words[2], 0, 1, 2);
        }
    }

    /// <summary>
    /// Displays a platform-native dialog with up to three buttons, using the appropriate native API
    /// for the current platform (Android alert dialog, WinUI content dialog, or an iOS/Mac Catalyst
    /// alert controller or form-sheet view controller), and awaits the user's selection.
    /// </summary>
    /// <typeparam name="T">The result type associated with each button.</typeparam>
    /// <param name="title">The dialog title.</param>
    /// <param name="content">The dialog content: a message string, or a view/element to embed.</param>
    /// <param name="primaryButtonText">The primary button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="secondaryButtonText">The secondary button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="closeButtonText">The close button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="primaryResult">The result returned if the primary button is selected.</param>
    /// <param name="secondaryResult">The result returned if the secondary button is selected.</param>
    /// <param name="closeResult">The result returned if the close button is selected.</param>
    /// <returns>The result associated with the button the user selected.</returns>
    /// <exception cref="NotImplementedException">Thrown on platforms without a supported native dialog implementation, or for unsupported content types.</exception>

    public async Task<T?> DisplayThreeButtonsAsync<T>(
         string? title, object? content,
         string? primaryButtonText, string? secondaryButtonText, string? closeButtonText,
         T primaryResult, T secondaryResult, T closeResult)
    {
        var taskCompletionSource = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The native platforms show the view as a platform view. The plain net10.0 target has no platform view: the view is
        // given as it is to a popup (see the end of the method), which is the caller's, so it is not disconnected below.
        Element? hosted = null;
#if ANDROID || WINDOWS || IOS || MACCATALYST
        hosted = content as Element;
        if (hosted is not null)
        {
            content = Microsoft.Maui.Platform.ElementExtensions.ToPlatform(hosted, windowContext.CurrentPage?.Handler?.MauiContext ?? new MauiContext(provider));
        }
#endif

        try
        {
#if ANDROID
        var alertBuilder = new Android.App.AlertDialog.Builder(Platform.CurrentActivity);

        alertBuilder.SetTitle(title);
        if (content is string message)
            alertBuilder.SetMessage(message);
        else
            alertBuilder.SetView(content as Android.Views.View);

        if (!string.IsNullOrEmpty(primaryButtonText))
            alertBuilder.SetPositiveButton(primaryButtonText, (senderAlert, args) =>
            {
                taskCompletionSource.TrySetResult(primaryResult);
            });
        if (!string.IsNullOrEmpty(secondaryButtonText))
            alertBuilder.SetNegativeButton(secondaryButtonText, (senderAlert, args) =>
            {
                taskCompletionSource.TrySetResult(secondaryResult);
            });
        if (!string.IsNullOrEmpty(closeButtonText))
            alertBuilder.SetNeutralButton(closeButtonText, (senderAlery, args) =>
            {
                taskCompletionSource.TrySetResult(closeResult);
            });

        var dialog = alertBuilder.Create();
        // Tapping outside the dialog or pressing Back closes it without a button.
        if (dialog is not null)
            dialog.CancelEvent += (_, _) => taskCompletionSource.TrySetResult(closeResult);
        dialog?.Show();
        var result = await taskCompletionSource.Task;
        return result;
#elif WINDOWS
        var current = windowContext.CurrentPage ?? throw new InvalidOperationException(
                "No active Page to host the content dialog popup.");
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            RequestedTheme = Application.Current?.RequestedTheme ==
         AppTheme.Dark ? Microsoft.UI.Xaml.ElementTheme.Dark :
         Application.Current?.RequestedTheme == AppTheme.Light ?
         Microsoft.UI.Xaml.ElementTheme.Light : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            CloseButtonText = closeButtonText,
            XamlRoot = current.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement page ? page.XamlRoot : null
        };
        dialog.Closed += (s, e) => taskCompletionSource.TrySetResult(default);
        dialog.PrimaryButtonClick += (s, e) => taskCompletionSource.TrySetResult(primaryResult);
        dialog.SecondaryButtonClick += (s, e) => taskCompletionSource.TrySetResult(secondaryResult);
        dialog.CloseButtonClick += (s, e) => taskCompletionSource.TrySetResult(closeResult);
        // WinUI allows one ContentDialog at a time per window: a second ShowAsync throws. Wait for the one that is open.
        await WindowsDialogGate.WaitAsync();
        try { await dialog.ShowAsync(); }
        finally { WindowsDialogGate.Release(); }
        return await taskCompletionSource.Task;

#elif MACCATALYST || IOS
        if (content is string message)
        {
            var alert = new UIKit.UIAlertController
            {
                Title = title,
                Message = message,
                //PreferredStyle = UIKit.UIAlertControllerStyle.Alert
            };
            if (!string.IsNullOrEmpty(primaryButtonText))
                alert.AddAction(UIKit.UIAlertAction.Create(primaryButtonText, UIKit.UIAlertActionStyle.Default, _ =>
                {
                    taskCompletionSource.TrySetResult(primaryResult);
                }));
            if (!string.IsNullOrEmpty(secondaryButtonText))
                alert.AddAction(UIKit.UIAlertAction.Create(secondaryButtonText, UIKit.UIAlertActionStyle.Default, _ =>
                {
                    taskCompletionSource.TrySetResult(secondaryResult);
                }));
            if (!string.IsNullOrEmpty(closeButtonText))
                alert.AddAction(UIKit.UIAlertAction.Create(closeButtonText, UIKit.UIAlertActionStyle.Default, _ =>
                {
                    taskCompletionSource.TrySetResult(closeResult);
                }));
            var keyWindow = UIKit.UIApplication.SharedApplication.ConnectedScenes
                                .OfType<UIKit.UIWindowScene>()
                                .SelectMany(s => s.Windows)
                                .FirstOrDefault(w => w.IsKeyWindow);
            var rootViewController = keyWindow?.RootViewController
                ?? throw new InvalidOperationException("No window to present the alert; the caller would wait for ever.");
            rootViewController.PresentViewController(alert, true, null);
            return await taskCompletionSource.Task;
        }
        else if (content is UIKit.UIView contentView)
        {
            var vc = new UIKit.UIViewController { ModalPresentationStyle = UIKit.UIModalPresentationStyle.FormSheet };
            vc.PreferredContentSize = new CoreGraphics.CGSize(320, 360);

            var stack = new UIKit.UIStackView { Axis = UIKit.UILayoutConstraintAxis.Vertical, Spacing = 12 };
            stack.TranslatesAutoresizingMaskIntoConstraints = false;

            if (!string.IsNullOrEmpty(title))
            {
                var label = new UIKit.UILabel
                {
                    Text = title
                };
                if (UIKit.UIFont.BoldSystemFontOfSize(17) is { } font)
                {
                    label.Font = font;
                }
                else if (UIKit.UIFont.SystemFontOfSize(17) is { } font2)
                {
                    label.Font = font2;
                }
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

            void AddButton(string? text, T result)
            {
                if (string.IsNullOrEmpty(text)) return;
                var button = UIKit.UIButton.FromType(UIKit.UIButtonType.System);
                button.SetTitle(text, UIKit.UIControlState.Normal);
                button.TouchUpInside += (_, _) => { taskCompletionSource.TrySetResult(result); vc.DismissViewController(true, null); };
                buttons.AddArrangedSubview(button);
            }
            AddButton(primaryButtonText, primaryResult);
            AddButton(secondaryButtonText, secondaryResult);
            AddButton(closeButtonText, closeResult);
            stack.AddArrangedSubview(buttons);

            vc.View!.AddSubview(stack);
            UIKit.NSLayoutConstraint.ActivateConstraints(new[]
            {
                    stack.LeadingAnchor.ConstraintEqualTo(vc.View.LeadingAnchor, 16),
                    stack.TrailingAnchor.ConstraintEqualTo(vc.View.TrailingAnchor, -16),
                    stack.TopAnchor.ConstraintEqualTo(vc.View.TopAnchor, 16),
                    stack.BottomAnchor.ConstraintEqualTo(vc.View.BottomAnchor, -16),
                });

            // The sheet was built but never presented, so the caller waited for ever.
            var top = UIKit.UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIKit.UIWindowScene>()
                .SelectMany(s => s.Windows)
                .FirstOrDefault(w => w.IsKeyWindow)?.RootViewController;
            while (top?.PresentedViewController is { } presented)
                top = presented;

            if (top is null)
                return closeResult;

            // Only the buttons close it: a swipe down would dismiss it without a result and leave the caller waiting.
            vc.ModalInPresentation = true;
            top.PresentViewController(vc, true, null);
            return await taskCompletionSource.Task;
        }

#endif
#if ANDROID || WINDOWS || MACCATALYST || IOS
        throw new NotImplementedException();
#else
            // No native dialog on this target (the plain net10.0 one, which is what a platform without a MAUI backend of its own
            // runs): the three buttons are those of a content dialog, drawn by the content dialog service with MAUI views.
            var dialog = new RouteableContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText
            };

            var result = await provider.GetRequiredService<IRouteableContentDialogService>().ShowAsync(dialog);
            return result switch
            {
                RouteableContentDialogResult.Primary => primaryResult,
                RouteableContentDialogResult.Secondary => secondaryResult,
                _ => closeResult
            };
#endif
        }
        finally
        {
            // The platform view was created for this dialog only: its handler holds it (and the element) until it is
            // disconnected.
            hosted?.Handler?.DisconnectHandler();
        }
    }
}