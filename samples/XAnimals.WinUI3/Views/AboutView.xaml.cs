using Microsoft.UI.Xaml;
using Routeables.WinUI3;
using XAnimals.ViewModels;

namespace XAnimals.WinUI3.Views;

public sealed partial class AboutView : RouteableFor
{
    public AboutView()
    {
        InitializeComponent();
    }

    private async void OnLearnMoreClick(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri(AboutViewModel.LearnMoreUrl)); }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}"); }
    }
}
