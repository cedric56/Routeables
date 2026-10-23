#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Routeables.Abstraction;
using Routeables.Services;
using Application = Microsoft.Maui.Controls.Application;
using Color = Android.Graphics.Color;
using ImageButton = Android.Widget.ImageButton;
using Paint = Android.Graphics.Paint;
using View = Android.Views.View;
namespace Routeables.Maui.Services;

static class RouteableToastHelper
{
    const string HostTag = "Routeables.ToastHost";

    public static View? Show(
        string title,
        string message,
        RouteableNotificationType type,
        TimeSpan? expiration,
        Action? onClick,
        Action? onClose)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current Activity to attach the notification.");

        var host = GetOrCreateHost(activity)
            ?? throw new InvalidOperationException("No root content view to attach the notification.");

        var initialTheme = ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Light);
        var accentArgb = GetAccent(type, initialTheme);
        var glyph = GetGlyph(type);

        var (card, closeButton, applyTheme) = BuildCard(activity, title, message, accentArgb, glyph);
        applyTheme(initialTheme);

        void OnThemeChanged(object? s, AppThemeChangedEventArgs e) => applyTheme(ResolveTheme(e.RequestedTheme));
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += OnThemeChanged;

        var session = new RouteableNotificationSession(title, message, type, expiration, onClick, onClose);
        var released = false;
        var handler = new Handler(Looper.MainLooper!);
        Java.Lang.Runnable? pendingDismiss = null;

        // What ties the card to the application: the theme subscription and the delayed dismissal. Let go of both when the
        // toast is dismissed or when its view is torn down (an activity destroyed before the toast was dismissed), or the
        // application would keep the card, and the activity it belongs to, alive.
        void Release()
        {
            if (released) return;
            released = true;

            if (pendingDismiss is not null)
                handler.RemoveCallbacks(pendingDismiss);   // the same Runnable that was posted, or nothing is removed
            pendingDismiss = null;

            if (Application.Current is not null)
                Application.Current.RequestedThemeChanged -= OnThemeChanged;
        }

        void Dismiss()
        {
            if (session.IsClosed) return;

            Release();

            AnimateOut(card, () =>
            {
                host.RemoveView(card);
                session.NotifyClosed();
            });
        }

        // A tap is the answer to the notification: it runs onClick, if any, and dismisses it.
        card.Click += (_, _) =>
        {
            session.Click();
            Dismiss();
        };

        closeButton.Click += (_, _) => Dismiss();
        card.ViewDetachedFromWindow += (_, _) => Release();

        if (session.AutoCloseAfter is { } delay)
        {
            pendingDismiss = new Java.Lang.Runnable(Dismiss);
            handler.PostDelayed(pendingDismiss, (long)delay.TotalMilliseconds);
        }

        host.AddView(card, 0); // newest on top
        AnimateIn(card);

        return card;
    }

    static AppTheme ResolveTheme(AppTheme theme) => theme == AppTheme.Unspecified
        ? Application.Current?.PlatformAppTheme ?? AppTheme.Light
        : theme;

    // Accent colors stay the same hue across themes, slightly brighter in dark mode so
    // they read clearly against a dark card — mirrors the Windows ToastHelper palette.
    static Color GetAccent(RouteableNotificationType type, AppTheme theme)
    {
        var dark = theme == AppTheme.Dark;
        return type switch
        {
            RouteableNotificationType.Success => dark ? Color.Argb(255, 0x4C, 0xC2, 0x6A) : Color.Argb(255, 0x2E, 0xA0, 0x44),
            RouteableNotificationType.Warning => dark ? Color.Argb(255, 0xF5, 0xB7, 0x5C) : Color.Argb(255, 0xE0, 0x9F, 0x3E),
            RouteableNotificationType.Error => dark ? Color.Argb(255, 0xFF, 0x5C, 0x72) : Color.Argb(255, 0xE8, 0x1E, 0x3E),
            _ => dark ? Color.Argb(255, 0x6C, 0xA6, 0xF5) : Color.Argb(255, 0x3E, 0x7B, 0xE0),
        };
    }

    static string GetGlyph(RouteableNotificationType type) => type switch
    {
        RouteableNotificationType.Success => "\u2713", // ✓
        RouteableNotificationType.Warning => "\u26A0", // ⚠
        RouteableNotificationType.Error => "\u2715",   // ✕
        _ => "\u2139",                        // ℹ
    };

    readonly record struct CardPalette(Color Background, Color Border, Color Title, Color Message, Color CloseGlyph);

    static CardPalette GetPalette(AppTheme theme) => theme == AppTheme.Dark
        ? new CardPalette(
            Background: Color.Argb(0xFA, 0x2C, 0x2C, 0x2E),
            Border: Color.Argb(0x30, 0xFF, 0xFF, 0xFF),
            Title: Color.Argb(0xFF, 0xFF, 0xFF, 0xFF),
            Message: Color.Argb(0xC0, 0xFF, 0xFF, 0xFF),
            CloseGlyph: Color.Argb(0xFF, 0xFF, 0xFF, 0xFF))
        : new CardPalette(
            Background: Color.Argb(0xFA, 0xFF, 0xFF, 0xFF),
            Border: Color.Argb(0x20, 0x00, 0x00, 0x00),
            Title: Color.Argb(0xFF, 0x1B, 0x1B, 0x1B),
            Message: Color.Argb(0xC0, 0x1B, 0x1B, 0x1B),
            CloseGlyph: Color.Argb(0xFF, 0x1B, 0x1B, 0x1B));

    static int Dp(Context context, float dp) =>
        (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, context.Resources!.DisplayMetrics);

    /// <summary>
    /// Builds the card: accent bar + icon + title/message + close button, in a rounded,
    /// elevated container. Returns the root view to animate/host, the close button, and an
    /// applyTheme delegate that re-colors the card's chrome — call it once up front and
    /// again whenever the app theme changes.
    /// </summary>
    static (LinearLayout Card, ImageButton CloseButton, Action<AppTheme> ApplyTheme) BuildCard(
        Context context, string title, string message, Color accent, string glyph)
    {
        var cardWidthDp = Dp(context, 340);
        var backgroundDrawable = new GradientDrawable();
        backgroundDrawable.SetShape(ShapeType.Rectangle);
        backgroundDrawable.SetCornerRadius(Dp(context, 8)); // rounded on all four corners now
        backgroundDrawable.SetStroke(Dp(context, 1), Color.Transparent);

        var icon = new TextView(context)
        {
            Text = glyph,
            TextSize = 18, // sp — TextView.TextSize already expects sp, do not run through Sp()
        };
        icon.SetTextColor(accent);
        icon.SetTypeface(Android.Graphics.Typeface.DefaultBold, TypefaceStyle.Bold);
        icon.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        {
            RightMargin = Dp(context, 12),
            Gravity = GravityFlags.Top,
        };

        var titleView = new TextView(context)
        {
            Text = title,
            TextSize = 14, // sp
        };
        titleView.SetTypeface(Android.Graphics.Typeface.DefaultBold, TypefaceStyle.Bold);

        var messageView = new TextView(context)
        {
            Text = message,
            TextSize = 13, // sp
        };
        messageView.SetPadding(0, Dp(context, 2), 0, 0);

        var textStack = new LinearLayout(context) { Orientation = Orientation.Vertical };
        textStack.AddView(titleView);
        textStack.AddView(messageView);
        textStack.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        {
            Gravity = GravityFlags.CenterVertical,
        };

        var closeButton = new ImageButton(context);
        closeButton.SetImageDrawable(CreateCloseGlyphDrawable(context, Color.Black));
        closeButton.SetBackgroundColor(Color.Transparent);
        closeButton.SetPadding(Dp(context, 4), Dp(context, 4), Dp(context, 4), Dp(context, 4));
        closeButton.LayoutParameters = new LinearLayout.LayoutParams(Dp(context, 28), Dp(context, 28))
        {
            Gravity = GravityFlags.Top | GravityFlags.End,
        };

        // Holds icon + text + close button, inset away from the card's edges and from the
        // accent bar. The card's rounded background shows through around this content.
        var content = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        content.SetPadding(Dp(context, 16), Dp(context, 12), Dp(context, 12), Dp(context, 12));
        content.AddView(icon);
        content.AddView(textStack);
        content.AddView(closeButton);
        content.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

        // The accent bar is a SIBLING of `content` inside a horizontal LinearLayout, not an
        // overlay inside a FrameLayout. A horizontal LinearLayout explicitly re-measures a
        // MatchParent-height child against its own final (sibling-derived) height, which is
        // exactly "as tall as the card" here. A FrameLayout has no such mechanism — a
        // MatchParent child there measures against the nearest *constrained* ancestor, which
        // was the whole screen, producing the full-height line seen in testing.
        var accentBar = new View(context)
        {
            LayoutParameters = new LinearLayout.LayoutParams(Dp(context, 4), ViewGroup.LayoutParams.MatchParent),
        };
        var accentDrawable = new GradientDrawable();
        accentDrawable.SetShape(ShapeType.Rectangle);
        accentDrawable.SetCornerRadii([Dp(context, 2), Dp(context, 2), 0, 0, 0, 0, Dp(context, 2), Dp(context, 2)]);
        accentDrawable.SetColor(accent);
        accentBar.Background = accentDrawable;

        var card = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        card.Background = backgroundDrawable;
        card.ClipToOutline = true; // keep the accent bar's square inner edge inside the card's rounded corners
        card.AddView(accentBar);
        card.AddView(content);
        card.LayoutParameters = new ViewGroup.MarginLayoutParams(cardWidthDp, ViewGroup.LayoutParams.WrapContent)
        {
            BottomMargin = Dp(context, 10),
        };
        card.Elevation = Dp(context, 6);
        card.Clickable = true;
        card.Focusable = true;

        void ApplyTheme(AppTheme theme)
        {
            var palette = GetPalette(ResolveTheme(theme));
            backgroundDrawable.SetColor(palette.Background);
            backgroundDrawable.SetStroke(Dp(context, 1), palette.Border);
            titleView.SetTextColor(palette.Title);
            messageView.SetTextColor(palette.Message);
            closeButton.SetImageDrawable(CreateCloseGlyphDrawable(context, palette.CloseGlyph));
        }

        return (card, closeButton, ApplyTheme);
    }

    /// <summary>Draws a simple "×" glyph as a Drawable, avoiding a dependency on a vector icon resource.</summary>
    static Drawable CreateCloseGlyphDrawable(Context context, Color color)
    {
        var size = Dp(context, 16);
        using var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        using var paint = new Paint(PaintFlags.AntiAlias) { Color = color, StrokeWidth = Dp(context, 1.5f) };
        paint.SetStyle(Paint.Style.Stroke);
        var pad = size * 0.25f;
        canvas.DrawLine(pad, pad, size - pad, size - pad, paint);
        canvas.DrawLine(size - pad, pad, pad, size - pad, paint);
        return new BitmapDrawable(context.Resources, bitmap);
    }

    static void AnimateIn(View card)
    {
        card.Alpha = 0f;
        card.TranslationX = Dp(card.Context!, 60);
        card?.Animate()?
            .Alpha(1f)
            .TranslationX(0)
            .SetDuration(220)
            .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator())
            .Start();
    }

    static void AnimateOut(View card, Action onComplete)
    {
        card?.Animate()?
            .Alpha(0f)
            .SetDuration(160)
            .SetInterpolator(new Android.Views.Animations.AccelerateInterpolator())
            .WithEndAction(new Java.Lang.Runnable(onComplete))
            .Start();
    }

    static LinearLayout? _host;

    static LinearLayout? GetOrCreateHost(Activity activity)
    {
        if (_host is { IsAttachedToWindow: true })
            return _host;

        var decor = activity.Window?.DecorView as ViewGroup;
        if (decor is null) return null;

        var existing = decor.FindViewWithTag(HostTag) as LinearLayout;
        if (existing is not null)
            return _host = existing;

        // Vertical LinearLayout, not FrameLayout: cards must stack top-to-bottom and
        // shift to fill the gap when one is removed, the same way the Windows
        // StackPanel-based host behaves. A FrameLayout would overlap them instead.
        var host = new LinearLayout(activity) { Tag = HostTag, Orientation = Orientation.Vertical };

        var layoutParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Top | GravityFlags.End)
        {
            TopMargin = Dp(activity, 16) + GetStatusBarHeight(activity),
            RightMargin = Dp(activity, 16),
        };

        decor.AddView(host, layoutParams);
        return _host = host;
    }

    static int GetStatusBarHeight(Context context)
    {
        var resourceId = context.Resources!.GetIdentifier("status_bar_height", "dimen", "android");
        return resourceId > 0 ? context.Resources.GetDimensionPixelSize(resourceId) : 0;
    }
}
#endif
