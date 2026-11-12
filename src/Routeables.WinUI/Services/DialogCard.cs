using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Windows.Input;
using Application = Microsoft.UI.Xaml.Application;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

#if WINUI3
namespace Routeables.WinUI3.Services;
#else
namespace Routeables.Uno.Services;
#endif

/// <summary>A button of a dialog: its caption, whether it is the default one, whether it can be pressed, and what it does.</summary>
internal sealed class DialogAction(string text, Func<bool> submit, bool isDefault)
{
    private bool _isEnabled = true;

    public string Text { get; } = text;

    public bool IsDefault { get; } = isDefault;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when <see cref="IsEnabled"/> changes.</summary>
    public event Action? Changed;

    public void Invoke() => submit();
}

/// <summary>What a dialog shows: the body, and its buttons.</summary>
internal sealed record DialogCardContent(StackPanel Body, IReadOnlyList<DialogAction> Actions);

/// <summary>The pieces the dialogs are made of.</summary>
internal static class DialogCard
{
    /// <summary>The parts one under the other; the buttons are the ones of the <c>ContentDialog</c> (see <see cref="DialogPresenter"/>).</summary>
    public static DialogCardContent Create(IEnumerable<UIElement?> parts, IEnumerable<DialogAction> actions)
    {
        var card = new StackPanel { Spacing = 16, MinWidth = 320, MaxWidth = 560 };
        foreach (var part in parts)
            if (part is not null)
                card.Children.Add(part);

        return new DialogCardContent(card, [.. actions]);
    }

    /// <summary>A button that runs <paramref name="submit"/> when it is pressed.</summary>
    public static DialogAction CreateButton(string text, Func<bool> submit, bool isDefault = false) => new(text, submit, isDefault);

    /// <summary>
    /// A button tied to a command: it is enabled while the command can execute (the changes are followed until <paramref name="cleanup"/> runs).
    /// <paramref name="submit"/> runs the command or the callback, and closes the dialog.
    /// </summary>
    public static DialogAction CreateCommandButton(
        string text,
        bool isEnabled,
        ICommand? command,
        object? parameter,
        Func<bool> submit,
        List<Action> cleanup,
        bool isDefault = false)
    {
        var action = CreateButton(text, submit, isDefault);
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        void Update() => action.IsEnabled = isEnabled && (command is null || command.CanExecute(parameter));
        Update();

        if (command is not null)
        {
            EventHandler changed = (_, _) =>
            {
                if (queue is { HasThreadAccess: false })
                    queue.TryEnqueue(Update);
                else
                    Update();
            };
            command.CanExecuteChanged += changed;
            cleanup.Add(() => command.CanExecuteChanged -= changed);
        }

        return action;
    }

    /// <summary>
    /// The buttons as a sheet under the card, one under the other and as wide as the card (the action sheet of a phone): for a dialog with more than the three buttons a
    /// <c>ContentDialog</c> has. The last one is the way out (what Escape does), set apart from the choices.
    /// </summary>
    public static void AddButtonSheet(StackPanel card, IEnumerable<DialogAction> actions)
    {
        var list = actions.ToList();
        if (list.Count == 0)
            return;

        var sheet = new StackPanel { Spacing = 8 };
        for (var i = 0; i < list.Count; i++)
        {
            var action = list[i];
            var button = new Button
            {
                Content = action.Text,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsEnabled = action.IsEnabled,
                // The way out is apart from the choices.
                Margin = i == list.Count - 1 && list.Count > 1 ? new Thickness(0, 8, 0, 0) : default
            };
            if (action.IsDefault
                && Application.Current?.Resources.TryGetValue("AccentButtonStyle", out var style) == true
                && style is Style accent)
            {
                button.Style = accent;
            }

            button.Click += (_, _) => action.Invoke();
            action.Changed += () => button.IsEnabled = action.IsEnabled;
            sheet.Children.Add(button);
        }

        card.Children.Add(sheet);
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
