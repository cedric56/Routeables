using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace Routeables.Maui.Services;

/// <summary>One button of a dialog.</summary>
/// <param name="Text">The button caption.</param>
/// <param name="IsPrimary">Whether it is the main, accented button.</param>
/// <param name="IsEnabled">Whether it can be tapped.</param>
/// <param name="Tap">What to do when it is tapped.</param>
/// <param name="Command">A command to bind instead: the button then follows its CanExecute, and <paramref name="Tap"/> is not used.</param>
public sealed record DialogButton(string Text, bool IsPrimary, bool IsEnabled, Func<Task>? Tap = null, ICommand? Command = null);

/// <summary>
/// Dresses the dialog popups like the platform's own dialogs (WinUI ContentDialog, Material AlertDialog,
/// iOS alert) while they stay popups, so several can be open at once.
/// </summary>
public static class RouteableNativeDialogLook
{
    private enum Look { Windows, Android, Ios }

    private static Look Current =>
        DeviceInfo.Platform == DevicePlatform.Android ? Look.Android :
        DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.MacCatalyst ? Look.Ios :
        Look.Windows;

    /// <summary>The corner radius of the dialog.</summary>
    public static double CornerRadius => Current switch { Look.Android => 28, Look.Ios => 14, _ => 8 };

    /// <summary>The color of links inside a dialog ("More details").</summary>
    public static void ApplyLinkColor(Button button)
    {
        switch (Current)
        {
            case Look.Android: button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#6750A4"), Color.FromArgb("#D0BCFF")); break;
            case Look.Ios: button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#007AFF"), Color.FromArgb("#0A84FF")); break;
            default: button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#0067C0"), Color.FromArgb("#60CDFF")); break;
        }
    }

    /// <summary>The accent color of a progress bar.</summary>
    public static void ApplyAccentColor(ProgressBar bar)
    {
        switch (Current)
        {
            case Look.Android: bar.SetAppThemeColor(ProgressBar.ProgressColorProperty, Color.FromArgb("#6750A4"), Color.FromArgb("#D0BCFF")); break;
            case Look.Ios: bar.SetAppThemeColor(ProgressBar.ProgressColorProperty, Color.FromArgb("#007AFF"), Color.FromArgb("#0A84FF")); break;
            default: bar.SetAppThemeColor(ProgressBar.ProgressColorProperty, Color.FromArgb("#0067C0"), Color.FromArgb("#60CDFF")); break;
        }
    }

    /// <summary>A label in the dialog's text colors; secondary text is the softer one.</summary>
    public static Label Text(string? text, double size = 14, FontAttributes attributes = FontAttributes.None, bool secondary = false)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = attributes,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = Current == Look.Ios ? TextAlignment.Center : TextAlignment.Start
        };
        switch (Current)
        {
            case Look.Android:
                label.SetAppThemeColor(Label.TextColorProperty,
                    Color.FromArgb(secondary ? "#49454F" : "#1D1B20"), Color.FromArgb(secondary ? "#CAC4D0" : "#E6E0E9"));
                break;
            case Look.Ios:
                label.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
                break;
            default:
                label.SetAppThemeColor(Label.TextColorProperty,
                    Color.FromArgb(secondary ? "#5D5D5D" : "#1A1A1A"), Color.FromArgb(secondary ? "#C5C5C5" : "#FFFFFF"));
                break;
        }
        return label;
    }

    /// <summary>
    /// Builds the dialog: a title, the body, and the buttons. The buttons are given in WinUI order
    /// (primary, secondary, close); Android and the two-button iOS alert put the primary one last.
    /// </summary>
    public static Border Build(string? title, View body, IReadOnlyList<DialogButton> buttons)
    {
        return Current switch
        {
            Look.Android => BuildAndroid(title, body, buttons.Reverse().ToList()),
            Look.Ios => BuildIos(title, body, buttons.Count == 2 ? buttons.Reverse().ToList() : buttons),
            _ => BuildWindows(title, body, buttons)
        };
    }

    // ------------------------------------------------------------------ Windows (Fluent)

    private static Border BuildWindows(string? title, View body, IReadOnlyList<DialogButton> buttons)
    {
        var content = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(24, 24, 24, 24) };
        if (!string.IsNullOrWhiteSpace(title))
            content.Add(Text(title, 20, FontAttributes.Bold));
        content.Add(body);

        // Equal columns, as WinUI does, wide enough for the longest caption.
        var column = buttons.Count == 0 ? 0 : buttons.Max(b => ButtonWidth(b, 88));
        var commands = new Grid { ColumnSpacing = 8, Padding = new Thickness(24, 24), MinimumWidthRequest = buttons.Count * column + (buttons.Count - 1) * 8 + 48 };
        for (var i = 0; i < buttons.Count; i++)
        {
            commands.AddColumnDefinition(new ColumnDefinition(GridLength.Star));
            commands.Add(WindowsButton(buttons[i]), i, 0);
        }
        commands.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#E9E9E9"), Color.FromArgb("#2C2C2C"));

        var root = new VerticalStackLayout { Spacing = 0, Children = { content } };
        if (buttons.Count > 0)
            root.Add(commands);

        return Frame(root, 8, Color.FromArgb("#F3F3F3"), Color.FromArgb("#202020"), minWidth: Math.Max(320, commands.MinimumWidthRequest), maxWidth: Math.Max(548, commands.MinimumWidthRequest));
    }

    private static Button WindowsButton(DialogButton b)
    {
        var button = new Button
        {
            Text = b.Text,
            HeightRequest = 32,
            CornerRadius = 4,
            Padding = new Thickness(12, 0),
            FontSize = 14,
            BorderWidth = 1
        };
        if (b.IsPrimary)
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty, Color.FromArgb("#0067C0"), Color.FromArgb("#60CDFF"));
            button.SetAppThemeColor(Button.BorderColorProperty, Color.FromArgb("#0067C0"), Color.FromArgb("#60CDFF"));
            button.SetAppThemeColor(Button.TextColorProperty, Colors.White, Colors.Black);
        }
        else
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty, Color.FromArgb("#FDFDFD"), Color.FromArgb("#3A3A3A"));
            button.SetAppThemeColor(Button.BorderColorProperty, Color.FromArgb("#E0E0E0"), Color.FromArgb("#484848"));
            button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#1A1A1A"), Colors.White);
        }
        Wire(button, b);
        return button;
    }

    // ------------------------------------------------------------------ Android (Material 3)

    private static Border BuildAndroid(string? title, View body, IReadOnlyList<DialogButton> buttons)
    {
        var root = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(24) };
        if (!string.IsNullOrWhiteSpace(title))
            root.Add(Text(title, 24));
        root.Add(body);

        var row = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var b in buttons)
        {
            var button = new Button
            {
                Text = b.Text,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                FontSize = 14,
                HeightRequest = 40,
                MinimumWidthRequest = 64,
                Padding = new Thickness(12, 0)
            };
            button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#6750A4"), Color.FromArgb("#D0BCFF"));
            Wire(button, b);
            row.Add(button);
        }
        root.Add(row);

        return Frame(root, 28, Color.FromArgb("#ECE6F0"), Color.FromArgb("#2B2930"), minWidth: 280, maxWidth: 560);
    }

    // ------------------------------------------------------------------ iOS (alert)

    private static Border BuildIos(string? title, View body, IReadOnlyList<DialogButton> buttons)
    {
        var text = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(20, 20, 20, 16) };
        if (!string.IsNullOrWhiteSpace(title))
            text.Add(Text(title, 17, FontAttributes.Bold));
        text.Add(body);

        var root = new VerticalStackLayout { Spacing = 0, Children = { text } };

        if (buttons.Count == 2)
        {
            root.Add(Hairline());
            var row = new Grid { ColumnSpacing = 0, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            row.Add(IosButton(buttons[0]), 0, 0);
            row.Add(new BoxView { WidthRequest = 0.5, Color = Colors.Gray, Opacity = 0.5 }, 1, 0);
            row.Add(IosButton(buttons[1]), 2, 0);
            root.Add(row);
        }
        else
        {
            foreach (var b in buttons)
            {
                root.Add(Hairline());
                root.Add(IosButton(b));
            }
        }

        return Frame(root, 14, Color.FromArgb("#F2F2F2"), Color.FromArgb("#2C2C2E"), minWidth: 270, maxWidth: 270);
    }

    private static BoxView Hairline() => new() { HeightRequest = 0.5, Color = Colors.Gray, Opacity = 0.5 };

    private static Button IosButton(DialogButton b)
    {
        var button = new Button
        {
            Text = b.Text,
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            FontSize = 17,
            FontAttributes = b.IsPrimary ? FontAttributes.Bold : FontAttributes.None,
            HeightRequest = 44,
            CornerRadius = 0
        };
        button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#007AFF"), Color.FromArgb("#0A84FF"));
        Wire(button, b);
        return button;
    }

    // ------------------------------------------------------------------ shared

    private static void Wire(Button button, DialogButton b)
    {
        if (b.Command is not null)
        {
            // The button follows the command's CanExecute (a Save that waits for input, say).
            button.Command = b.Command;

            // The command (a view model's, long-lived) listens to CanExecuteChanged for the button: let go of it
            // when the dialog leaves the screen, so a closed dialog is not kept alive by it.
            button.Unloaded += (_, _) => button.Command = null;
            return;
        }

        button.IsEnabled = b.IsEnabled;

        // A second tap while the first is still closing the dialog would run the action twice; and the handler is async void:
        // an exception would end the application.
        var busy = false;
        button.Clicked += async (_, _) =>
        {
            if (busy || b.Tap is null)
                return;

            busy = true;
            try { await b.Tap(); }
            catch (Exception ex) { Routeables.Abstraction.RouteableErrors.Report(null, "Dialog.Button", ex); }
            finally { busy = false; }
        };
    }

    // A rough width for a caption, so a button is never cut.
    private static double ButtonWidth(DialogButton b, double minimum) => Math.Max(minimum, b.Text.Length * 9 + 32);

    private static Border Frame(View content, double radius, Color light, Color dark, double minWidth, double maxWidth)
    {
        var border = new Border
        {
            Padding = 0,
            StrokeThickness = Current == Look.Windows ? 1 : 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            MinimumWidthRequest = minWidth,
            MaximumWidthRequest = maxWidth,
            WidthRequest = minWidth == maxWidth ? minWidth : -1,
            Content = content,
            // Clipping to the rounded shape keeps the command area's corners round.
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        border.SetAppThemeColor(Border.BackgroundColorProperty, light, dark);
        border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#757575"), Color.FromArgb("#4A4A4A"));
        return border;
    }
}
