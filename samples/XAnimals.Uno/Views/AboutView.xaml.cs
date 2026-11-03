using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;
using Microsoft.UI.Xaml;

namespace XAnimals.Uno.Views;

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
