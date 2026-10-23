using Routeables.Abstraction;
using Routeables.Avalonia;
using Xaminals.ViewModels;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace XAnimals.Avalonia;

[RouteableView(typeof(MonkeyDetailViewModel), route: "/monkeys/{name}")]
public partial class MonkeyDetailView : RouteableFor<MonkeyDetailViewModel>
{
    public MonkeyDetailView()
    {
        InitializeComponent();
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.GoBackAsync();
    }
}