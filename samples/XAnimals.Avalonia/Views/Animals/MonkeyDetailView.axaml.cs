using Routeables.Abstraction;
using Routeables.Avalonia;
using Xaminals.ViewModels;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace XAnimals.Avalonia;

public partial class MonkeyDetailView : RouteableFor<MonkeyDetailViewModel>
{
    public MonkeyDetailView()
    {
        InitializeComponent();
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.GoBackAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}");
        }
    }
}