using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Windows.Input;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

/// <summary>Builds the content of a dialog (texts, body, buttons) with plain Avalonia controls.</summary>
internal static class DialogCard
{
    private const double Spacing = 12;

    /// <summary>Creates a button that runs <paramref name="onClick"/> when pressed.</summary>
    public static Button CreateButton(string text, Action onClick, bool isEnabled = true, bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
            IsEnabled = isEnabled,
            IsDefault = isDefault,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// Creates a button validated by an <see cref="ICommand"/> when there is one: it is enabled only while the
    /// command can execute (and follows <see cref="ICommand.CanExecuteChanged"/>), and pressing it does nothing while
    /// it cannot. Without a command the button is simply enabled and <paramref name="onClick"/> decides.
    /// </summary>
    /// <param name="text">The button text.</param>
    /// <param name="isEnabled">Whether the button is enabled at all, whatever the command says.</param>
    /// <param name="command">The command that validates the button, if any.</param>
    /// <param name="parameter">The parameter passed to the command.</param>
    /// <param name="onClick">Called when the button is pressed and, if there is a command, can execute.</param>
    /// <param name="cleanup">Receives the action that detaches the button from the command; run it when the dialog closes.</param>
    /// <param name="isDefault">Whether Enter activates this button.</param>
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
            IsDefault = isDefault,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        void Refresh() => button.IsEnabled = isEnabled && (command?.CanExecute(parameter) ?? true);
        Refresh();

        if (command is not null)
        {
            // CanExecuteChanged can be raised from any thread.
            void OnCanExecuteChanged(object? sender, EventArgs e)
            {
                if (Dispatcher.UIThread.CheckAccess()) Refresh();
                else Dispatcher.UIThread.Post(Refresh);
            }

            command.CanExecuteChanged += OnCanExecuteChanged;
            cleanup.Add(() => command.CanExecuteChanged -= OnCanExecuteChanged);
        }

        button.Click += (_, _) =>
        {
            if (command is not null && !command.CanExecute(parameter))
                return;

            onClick();
        };
        return button;
    }

    /// <summary>Turns the content of a dialog into a control: controls are used as they are, text is wrapped.</summary>
    public static Control? ToBody(object? content) => content switch
    {
        null => null,
        Control control => control,
        string text => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        // Any other object is shown through the application's data templates.
        _ => new ContentControl { Content = content }
    };

    /// <summary>Creates a small image or control from an icon source, when it is one of those.</summary>
    public static Control? ToIcon(object? iconSource) => iconSource switch
    {
        Control control => control,
        IImage image => new Image { Source = image, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Top },
        _ => null
    };

    /// <summary>Stacks the given parts vertically, with the buttons right-aligned at the bottom.</summary>
    public static Control Create(IEnumerable<Control?> parts, IEnumerable<Button> buttons)
    {
        var root = new StackPanel { Spacing = Spacing, Margin = new Thickness(20), MaxWidth = 560 };

        foreach (var part in parts)
        {
            if (part is not null)
                root.Children.Add(part);
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        foreach (var button in buttons)
            row.Children.Add(button);

        if (row.Children.Count > 0)
            root.Children.Add(row);

        return root;
    }

    /// <summary>Wraps a long body in a scroll viewer that stops growing at a reasonable height.</summary>
    public static ScrollViewer Scrollable(Control body) => new ScrollViewer
    {
        MaxHeight = 400,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = body
    };
}