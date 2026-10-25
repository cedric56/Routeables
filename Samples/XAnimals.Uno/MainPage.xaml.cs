using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XAnimals.ViewModels;
namespace XAnimals.Uno;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.ToggleTheme(ActualTheme == ElementTheme.Dark);
    }

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // Inverser l'état d'ouverture/fermeture du panneau
        RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
    }
}
