using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using XAminals.Models;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Wpf.Views;

/// <summary>
/// The list of any animal kind (search, selection, hosting of the detail view). The view that contains it
/// (<c>CatsView</c>, <c>BearsView</c>…) provides the view model as DataContext.
/// </summary>
public partial class AnimalList : UserControl
{
    private AnimalsViewModel? _viewModel;

    public AnimalList()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        Loaded += (_, _) => Attach();
    }

    /// <summary>Text shown when the search finds nothing ("No bears found.").</summary>
    public string NoResultsText { get; set; } = string.Empty;

    private void Attach()
    {
        Detach();
        _viewModel = DataContext as AnimalsViewModel;
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged += OnViewModelChanged;
        Refresh();
    }

    private void Detach()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnimalsViewModel.CurrentView) or nameof(AnimalsViewModel.FilteredAnimals))
        {
            if (Dispatcher.CheckAccess())
                Refresh();
            else
                Dispatcher.InvokeAsync(Refresh);
        }
    }

    // The detail covers the list while there is one; "no results" shows when the search finds nothing.
    private void Refresh()
    {
        var showingDetail = _viewModel?.CurrentView is not null;
        DetailHost.Visibility = showingDetail ? Visibility.Visible : Visibility.Collapsed;
        ListPanel.Visibility = showingDetail ? Visibility.Collapsed : Visibility.Visible;

        var empty = _viewModel is not null && _viewModel.FilteredAnimals.Count == 0;
        NoResults.Text = NoResultsText;
        NoResults.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (Animals.SelectedItem is Animal animal && _viewModel is not null)
            {
                // Clear the selection so the same animal can be opened again after going back.
                Animals.SelectedItem = null;
                await _viewModel.OnSelectedAnimalAsync(animal);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}");
        }
    }
}
