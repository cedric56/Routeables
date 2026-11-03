using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

public partial class AboutView : RouteableFor
{
    public AboutView()
    {
        InitializeComponent();
    }

    private async void OnLearnMoreClicked(object? sender, EventArgs e)
    {
        try { await Launcher.OpenAsync(AboutViewModel.LearnMoreUrl); }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}"); }
    }
}
