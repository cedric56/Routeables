using XAminals.Models;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

/// <summary>
/// The list of any animal kind (search, selection, hosting of the detail view). The view that contains it
/// (<c>CatsView</c>, <c>BearsView</c>…) provides the view model as BindingContext.
/// </summary>
public partial class AnimalList : ContentView
{
    public AnimalList()
    {
        InitializeComponent();
    }

    /// <summary>Text shown when the search finds nothing ("No bears found.").</summary>
    public string NoResultsText
    {
        get => noResults.Text;
        set => noResults.Text = value;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Animal animal || BindingContext is not AnimalsViewModel viewModel)
            return;

        // Clear the selection so the same animal can be opened again after going back.
        list.SelectedItem = null;
        await viewModel.OnSelectedAnimalAsync(animal);
    }
}
