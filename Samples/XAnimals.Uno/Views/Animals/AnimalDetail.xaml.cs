using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

/// <summary>The detail of any animal kind; the view that contains it provides the view model as DataContext.</summary>
public sealed partial class AnimalDetail : UserControl
{
    public AnimalDetail()
    {
        InitializeComponent();
    }

    private async void OnBack(object sender, RoutedEventArgs e)
    {
        if (DataContext is AnimalDetailViewModel viewModel)
            await viewModel.GoBackAsync();
    }
}
