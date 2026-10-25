using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OnThemeToggle(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.ToggleTheme(ActualThemeVariant == ThemeVariant.Dark);
    }
}
