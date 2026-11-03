#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using Routeables.Abstraction;
using Routeables.Services;
using UIKit;

namespace Routeables.Maui.Services;

/// <summary>
/// The toast of iOS and Mac Catalyst: a card drawn with UIKit and added to the key window, above everything the page shows
/// (dialogs and popups included). Cards stack at the top, newest first, and take the system colors, so they follow the theme.
/// </summary>
static class RouteableToastHelper
{
    const nint HostTag = 0x52544F41;    // "RTOA"
    const double CardWidth = 420;

    public static void Show(
        string title,
        string message,
        RouteableNotificationType type,
        TimeSpan? expiration,
        Action? onClick,
        Action? onClose)
    {
        // UIKit is the main thread's: whichever thread asks, the card is built there.
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
            Present(new RouteableNotificationSession(title, message, type, expiration, onClick, onClose)));
    }

    static void Present(RouteableNotificationSession session)
    {
        var host = GetOrCreateHost();
        if (host is null)
            return;

        var card = BuildCard(session);
        NSTimer? timer = null;

        void Dismiss()
        {
            if (session.IsClosed)
                return;

            timer?.Invalidate();
            timer = null;

            UIView.Animate(0.2, () => card.Alpha = 0, () =>
            {
                host.RemoveArrangedSubview(card);
                card.RemoveFromSuperview();

                // The last card takes its host with it.
                if (host.ArrangedSubviews.Length == 0)
                    host.RemoveFromSuperview();

                session.NotifyClosed();
            });
        }

        // A tap is the answer to the notification: it runs onClick, if any, and dismisses it.
        card.AddGestureRecognizer(new UITapGestureRecognizer(() =>
        {
            session.Click();
            Dismiss();
        }));

        card.Alpha = 0;
        host.InsertArrangedSubview(card, 0);   // newest on top
        UIView.Animate(0.2, () => card.Alpha = 1);

        if (session.AutoCloseAfter is { } delay)
            timer = NSTimer.CreateScheduledTimer(delay, _ => Dismiss());
    }

    static UIStackView? GetOrCreateHost()
    {
        var window = KeyWindow();
        if (window is null)
            return null;

        if (window.ViewWithTag(HostTag) is UIStackView existing)
            return existing;

        var host = new UIStackView
        {
            Tag = HostTag,
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        window.AddSubview(host);

        var safe = window.SafeAreaLayoutGuide;
        var width = host.WidthAnchor.ConstraintEqualTo((nfloat)CardWidth);
        width.Priority = 750;   // a narrow window wins: the margins below keep the card on screen

        NSLayoutConstraint.ActivateConstraints(
        [
            host.TopAnchor.ConstraintEqualTo(safe.TopAnchor, 12),
            host.CenterXAnchor.ConstraintEqualTo(window.CenterXAnchor),
            host.LeadingAnchor.ConstraintGreaterThanOrEqualTo(safe.LeadingAnchor, 12),
            host.TrailingAnchor.ConstraintLessThanOrEqualTo(safe.TrailingAnchor, -12),
            width
        ]);

        return host;
    }

    // The window the user works in: the key window of a connected scene that is on screen.
    static UIWindow? KeyWindow() =>
        UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .Where(scene => scene.ActivationState == UISceneActivationState.ForegroundActive)
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)
        ?? UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault();

    static UIView BuildCard(RouteableNotificationSession session)
    {
        var (glyph, color) = session.Type switch
        {
            RouteableNotificationType.Success => ("✓", UIColor.SystemGreen),
            RouteableNotificationType.Warning => ("!", UIColor.SystemOrange),
            RouteableNotificationType.Error => ("✕", UIColor.SystemRed),
            _ => ("i", UIColor.SystemBlue)
        };

        var icon = new UILabel
        {
            Text = glyph,
            Font = UIFont.BoldSystemFontOfSize(20),
            TextColor = color,
            TextAlignment = UITextAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        icon.WidthAnchor.ConstraintEqualTo(24).Active = true;

        var texts = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Spacing = 2 };
        if (!string.IsNullOrWhiteSpace(session.Title))
            texts.AddArrangedSubview(new UILabel
            {
                Text = session.Title,
                Font = UIFont.BoldSystemFontOfSize(15),
                TextColor = UIColor.Label,
                Lines = 0
            });

        texts.AddArrangedSubview(new UILabel
        {
            Text = session.Message,
            Font = UIFont.SystemFontOfSize(14),
            TextColor = UIColor.SecondaryLabel,
            Lines = 0
        });

        var row = new UIStackView(new UIView[] { icon, texts })
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Spacing = 12,
            Alignment = UIStackViewAlignment.Top,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var card = new UIView
        {
            BackgroundColor = UIColor.SecondarySystemBackground,
            IsAccessibilityElement = true,
            AccessibilityLabel = string.IsNullOrWhiteSpace(session.Title) ? session.Message : $"{session.Title}. {session.Message}"
        };
        card.Layer.CornerRadius = 12;
        card.Layer.ShadowColor = UIColor.Black.CGColor;
        card.Layer.ShadowOpacity = 0.25f;
        card.Layer.ShadowRadius = 8;
        card.Layer.ShadowOffset = new CGSize(0, 3);

        card.AddSubview(row);
        NSLayoutConstraint.ActivateConstraints(
        [
            row.TopAnchor.ConstraintEqualTo(card.TopAnchor, 12),
            row.BottomAnchor.ConstraintEqualTo(card.BottomAnchor, -12),
            row.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 16),
            row.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -16)
        ]);

        return card;
    }
}
#endif
