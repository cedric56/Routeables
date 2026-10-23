using Avalonia.Controls;
using XAminals.Models;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

/// <summary>
/// The list of any animal kind (search, selection, hosting of the detail view). The view that contains it
/// (<c>CatsView</c>, <c>BearsView</c>…) provides the view model as DataContext.
/// </summary>
public partial class AnimalList : UserControl
{
    public AnimalList()
    {
        InitializeComponent();
    }

    /// <summary>Text shown when the search finds nothing ("No bears found.").</summary>
    public string NoResultsText
    {
        get => noResults.Text ?? string.Empty;
        set => noResults.Text = value;
    }

    private async void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (lb.SelectedItem is Animal animal && DataContext is AnimalsViewModel viewModel)
        {
            // Clear the selection so the same animal can be opened again after going back.
            lb.SelectedItem = null;
            await viewModel.OnSelectedAnimalAsync(animal);
        }
    }
}
