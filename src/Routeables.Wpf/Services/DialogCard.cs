using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Routeables.Wpf.Services;

// The pieces of a dialog, drawn with plain WPF controls.
internal static class DialogCard
{
    private const double Gap = 12;

    public static Button CreateButton(string text, Action onClick, bool isEnabled = true, bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
            Padding = new Thickness(12, 4, 12, 4),
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
            Padding = new Thickness(12, 4, 12, 4),
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
                if (button.Dispatcher.CheckAccess()) Refresh();
                else button.Dispatcher.InvokeAsync(Refresh);
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

    public static UIElement? ToBody(object? content) => content switch
    {
        null => null,
        UIElement element => element,
        string text => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        // Any other object is shown through the application's data templates.
        _ => new ContentControl { Content = content }
    };

    public static UIElement? ToIcon(object? iconSource) => iconSource switch
    {
        UIElement element => element,
        ImageSource image => new Image { Source = image, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Top },
        _ => null
    };

    public static UIElement Create(IEnumerable<UIElement?> parts, IEnumerable<Button> buttons)
    {
        var root = new StackPanel { Margin = new Thickness(20), MaxWidth = 560 };

        foreach (var part in parts)
        {
            if (part is null)
                continue;

            if (part is FrameworkElement element)
                element.Margin = new Thickness(element.Margin.Left, element.Margin.Top, element.Margin.Right, element.Margin.Bottom + Gap);
            root.Children.Add(part);
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(row.Children.Count == 0 ? 0 : 8, 0, 0, 0);
            row.Children.Add(button);
        }

        if (row.Children.Count > 0)
            root.Children.Add(row);

        return root;
    }

    public static ScrollViewer Scrollable(UIElement body) => new ScrollViewer
    {
        MaxHeight = 400,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = body
    };
}
