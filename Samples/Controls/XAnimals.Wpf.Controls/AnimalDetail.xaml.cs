using System.Windows;
using System.Windows.Controls;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Wpf.Views;

/// <summary>The detail of any animal kind; the view that contains it provides the view model as DataContext.</summary>
public partial class AnimalDetail : UserControl
{
    public AnimalDetail()
    {
        InitializeComponent();
    }

    private async void OnBack(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is AnimalDetailViewModel viewModel)
                await viewModel.GoBackAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}");
        }
    }
}
