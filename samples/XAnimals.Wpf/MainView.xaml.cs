using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using XAnimals.ViewModels;

namespace XAnimals.Wpf;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    // The theme shown. Unknown until the first click: the application follows the setting of Windows (ThemeMode.System).
    private bool? dark;

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        var shown = dark ?? WindowsUsesDarkTheme();
        (DataContext as MainViewModel)?.ToggleTheme(shown);
        dark = !shown;
    }

    private static bool WindowsUsesDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is 0;
    }

    private void OnMenuToggle(object sender, RoutedEventArgs e)
    {
        // The content takes the place that is freed (its column is a star); the menu keeps its selection.
        var open = Menu.Visibility == Visibility.Visible;
        Menu.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        MenuColumn.Width = open ? new GridLength(0) : new GridLength(280);
    }
}
