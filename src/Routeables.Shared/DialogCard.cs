// WinUI 3 and Uno are what is left when neither WPF nor Avalonia is named.
#if !WPF && !AVALONIA
#define ROUTEABLE_WINUI
#endif
#if AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Windows.Input;
using Routeables.Services;
using Button = Avalonia.Controls.Button;
using Orientation = Avalonia.Layout.Orientation;
using Element = Avalonia.Controls.Control;
using Picture = Avalonia.Media.IImage;
#elif WPF
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Routeables.Services;
using Element = System.Windows.UIElement;
using Picture = System.Windows.Media.ImageSource;
#else
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Routeables.Services;
using System.Windows.Input;
using Application = Microsoft.UI.Xaml.Application;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Element = Microsoft.UI.Xaml.UIElement;
using Picture = Microsoft.UI.Xaml.Media.ImageSource;
#endif

// One file for Routeables.Wpf, Routeables.Avalonia, Routeables.Uno and Routeables.WinUI3, which link it (and define WPF or AVALONIA): the pieces of a dialog, drawn with plain controls.
// A control is an UIElement in WPF and WinUI and a Control in Avalonia, an image an ImageSource or an IImage (the aliases Element and Picture); the space between
// the parts is a margin to add in WPF and a Spacing of the panel in Avalonia and WinUI. WPF and Avalonia build the buttons of the card themselves; in WinUI the
// buttons are the ones of the ContentDialog (see DialogPresenter), so the card hands over DialogAction records instead.
#if AVALONIA
namespace Routeables.Avalonia.Services;
#elif WPF
namespace Routeables.Wpf.Services;
#elif WINUI3
namespace Routeables.WinUI3.Services;
#else
namespace Routeables.Uno.Services;
#endif

#if ROUTEABLE_WINUI
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

#endif
internal static class DialogCard
{
#if ROUTEABLE_WINUI
    /// <summary>The parts one under the other; the buttons are the ones of the <c>ContentDialog</c> (see <see cref="DialogPresenter"/>).</summary>
    public static DialogCardContent Create(IEnumerable<Element?> parts, IEnumerable<DialogAction> actions)
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

        RouteableCommandFollower.Follow(command, parameter, isEnabled, enabled => action.IsEnabled = enabled,
            update => { if (queue is { HasThreadAccess: false }) queue.TryEnqueue(() => update()); else update(); }, cleanup);

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

    /// <summary>The semi-bold style of a header.</summary>
    public static TextBlock Header(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
    };
#else
    private const double Gap = 12;

    public static Button CreateButton(string text, Action onClick, bool isEnabled = true, bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
#if !AVALONIA
            Padding = new Thickness(12, 4, 12, 4),
#endif
            IsEnabled = isEnabled,
            IsDefault = isDefault,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static Button CreateCommandButton(
        string text,
        bool isEnabled,
        ICommand? command,
        object? parameter,
        Action onClick,
        List<Action> cleanup,
        bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
#if !AVALONIA
            Padding = new Thickness(12, 4, 12, 4),
#endif
            IsDefault = isDefault,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        // CanExecuteChanged can be raised from any thread.
#if AVALONIA
        RouteableCommandFollower.Follow(command, parameter, isEnabled, enabled => button.IsEnabled = enabled,
            action => { if (Dispatcher.UIThread.CheckAccess()) action(); else Dispatcher.UIThread.Post(action); }, cleanup);
#else
        RouteableCommandFollower.Follow(command, parameter, isEnabled, enabled => button.IsEnabled = enabled,
            action => { if (button.Dispatcher.CheckAccess()) action(); else button.Dispatcher.InvokeAsync(action); }, cleanup);
#endif

        button.Click += (_, _) =>
        {
            if (command is not null && !command.CanExecute(parameter))
                return;

            onClick();
        };
        return button;
    }

    public static Element Create(IEnumerable<Element?> parts, IEnumerable<Button> buttons)
    {
#if AVALONIA
        var root = new StackPanel { Spacing = Gap, Margin = new Thickness(20), MaxWidth = 560 };
#else
        var root = new StackPanel { Margin = new Thickness(20), MaxWidth = 560 };
#endif

        foreach (var part in parts)
        {
            if (part is null)
                continue;

#if !AVALONIA
            if (part is FrameworkElement element)
                element.Margin = new Thickness(element.Margin.Left, element.Margin.Top, element.Margin.Right, element.Margin.Bottom + Gap);
#endif
            root.Children.Add(part);
        }

#if AVALONIA
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var button in buttons)
            row.Children.Add(button);
#else
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(row.Children.Count == 0 ? 0 : 8, 0, 0, 0);
            row.Children.Add(button);
        }
#endif

        if (row.Children.Count > 0)
            root.Children.Add(row);

        return root;
    }

#endif

    public static Element? ToBody(object? content) => content switch
    {
        null => null,
        Element element => element,
        string text => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        // Any other object is shown through the application's data templates.
        _ => new ContentControl { Content = content }
    };

#if ROUTEABLE_WINUI
    /// <summary>An icon for an icon source: an element, an image, an address, or a glyph.</summary>
    public static Element? ToIcon(object? source) => source switch
    {
        null => null,
        Element element => element,
        Picture image => new Image { Source = image, Width = 32, Height = 32 },
        Uri uri => new Image { Source = new BitmapImage(uri), Width = 32, Height = 32 },
        string text when text.Contains("://", StringComparison.Ordinal) || text.StartsWith("ms-appx:", StringComparison.Ordinal)
            => new Image { Source = new BitmapImage(new Uri(text)), Width = 32, Height = 32 },
        string text => new TextBlock { Text = text, FontSize = 32, VerticalAlignment = VerticalAlignment.Center },
        _ => null,
    };

    /// <summary>A body that scrolls when it is taller than the window.</summary>
    public static ScrollViewer Scrollable(Element body) => new()
    {
        Content = body,
        MaxHeight = 420,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
#else
    public static Element? ToIcon(object? iconSource) => iconSource switch
    {
        Element element => element,
        Picture image => new Image { Source = image, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Top },
        _ => null
    };

    public static ScrollViewer Scrollable(Element body) => new ScrollViewer
    {
        MaxHeight = 400,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = body
    };
#endif
}
