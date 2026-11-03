using System.Windows.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.Text;

namespace Routeables.Uno.Services;

/// <summary>The pieces the dialogs are made of.</summary>
internal static class DialogCard
{
    /// <summary>The parts one under the other, then a row of buttons on the right.</summary>
    public static StackPanel Create(IEnumerable<UIElement?> parts, IEnumerable<Microsoft.UI.Xaml.Controls.Button> buttons)
    {
        var card = new StackPanel { Spacing = 16, MinWidth = 320, MaxWidth = 560 };
        foreach (var part in parts)
            if (part is not null)
                card.Children.Add(part);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        foreach (var button in buttons)
            row.Children.Add(button);

        if (row.Children.Count > 0)
            card.Children.Add(row);

        return card;
    }

    /// <summary>A button that runs <paramref name="submit"/> when it is pressed.</summary>
    public static Button CreateButton(string text, Func<bool> submit, bool isDefault = false)
    {
        var button = new Button { Content = text, MinWidth = 88 };
        if (isDefault
            && Application.Current?.Resources.TryGetValue("AccentButtonStyle", out var style) == true
            && style is Style accent)
        {
            button.Style = accent;
        }

        button.Click += (_, _) => submit();
        return button;
    }

    /// <summary>
    /// A button tied to a command: it is enabled while the command can execute (the changes are followed until <paramref name="cleanup"/> runs).
    /// <paramref name="submit"/> runs the command or the callback, and closes the dialog.
    /// </summary>
    public static Button CreateCommandButton(
        string text,
        bool isEnabled,
        ICommand? command,
        object? parameter,
        Func<bool> submit,
        List<Action> cleanup,
        bool isDefault = false)
    {
        var button = CreateButton(text, submit, isDefault);

        void Update() => button.IsEnabled = isEnabled && (command is null || command.CanExecute(parameter));
        Update();

        if (command is not null)
        {
            EventHandler changed = (_, _) =>
            {
                if (button.DispatcherQueue is { } queue && !queue.HasThreadAccess)
                    queue.TryEnqueue(Update);
                else
                    Update();
            };
            command.CanExecuteChanged += changed;
            cleanup.Add(() => command.CanExecuteChanged -= changed);
        }

        return button;
    }

    /// <summary>The body for a content: text, an element, or an object the framework shows with its templates.</summary>
    public static UIElement? ToBody(object? content) => content switch
    {
        null => null,
        string text => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        UIElement element => element,
        _ => new ContentControl { Content = content },
    };

    /// <summary>A body that scrolls when it is taller than the window.</summary>
    public static ScrollViewer Scrollable(UIElement body) => new()
    {
        Content = body,
        MaxHeight = 420,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    /// <summary>An icon for an icon source: an element, an image, an address, or a glyph.</summary>
    public static UIElement? ToIcon(object? source) => source switch
    {
        null => null,
        UIElement element => element,
        ImageSource image => new Image { Source = image, Width = 32, Height = 32 },
        Uri uri => new Image { Source = new BitmapImage(uri), Width = 32, Height = 32 },
        string text when text.Contains("://", StringComparison.Ordinal) || text.StartsWith("ms-appx:", StringComparison.Ordinal)
            => new Image { Source = new BitmapImage(new Uri(text)), Width = 32, Height = 32 },
        string text => new TextBlock { Text = text, FontSize = 32, VerticalAlignment = VerticalAlignment.Center },
        _ => null,
    };

    /// <summary>The semi-bold style of a header.</summary>
    public static TextBlock Header(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
    };
}
