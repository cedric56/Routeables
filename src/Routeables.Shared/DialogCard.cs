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
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Routeables.Services;
using Element = System.Windows.UIElement;
using Picture = System.Windows.Media.ImageSource;
#endif

// One file for Routeables.Wpf and Routeables.Avalonia, which link it (and define WPF or AVALONIA): the pieces of a dialog, drawn with plain controls.
// A control is an UIElement in WPF and a Control in Avalonia, an image an ImageSource or an IImage (the aliases Element and Picture); the space between
// the parts is a margin to add in WPF and a Spacing of the panel in Avalonia.
#if AVALONIA
namespace Routeables.Avalonia.Services;
#else
namespace Routeables.Wpf.Services;
#endif

internal static class DialogCard
{
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

    public static Element? ToBody(object? content) => content switch
    {
        null => null,
        Element element => element,
        string text => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        // Any other object is shown through the application's data templates.
        _ => new ContentControl { Content = content }
    };

    public static Element? ToIcon(object? iconSource) => iconSource switch
    {
        Element element => element,
        Picture image => new Image { Source = image, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Top },
        _ => null
    };

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

    public static ScrollViewer Scrollable(Element body) => new ScrollViewer
    {
        MaxHeight = 400,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = body
    };
}
