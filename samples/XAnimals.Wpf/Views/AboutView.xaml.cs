using System.Diagnostics;
using System.Windows;
using Routeables.Wpf;
using XAnimals.ViewModels;

namespace XAnimals.Wpf.Views;

public partial class AboutView : RouteableFor
{
    public AboutView()
    {
        InitializeComponent();
    }

    // The shell opens the address in the browser of the user.
    private void OnLearnMoreClick(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(AboutViewModel.LearnMoreUrl) { UseShellExecute = true });
}
