using Avalonia;
using Avalonia.Controls;
using System;
using XAnimals.ViewModels;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace XAnimals.Avalonia;

/// <summary>The detail of any animal kind; the view that contains it provides the view model as DataContext.</summary>
public partial class AnimalDetail : UserControl
{
    public AnimalDetail()
    {
        InitializeComponent();
        SizeChanged += (_, e) => FitHero(e.NewSize);
    }

    // The arch is 260 x 330 where there is room. On a phone it must leave the text in sight: at most 55 % of the width and
    // 40 % of the height, same proportions, corners kept round.
    private void FitHero(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        const double FullWidth = 260, Ratio = 330d / 260;
        var width = Math.Min(FullWidth, size.Width * 0.55);
        if (width * Ratio > size.Height * 0.4) width = size.Height * 0.4 / Ratio;
        Hero.Width = width;
        Hero.Height = width * Ratio;
        Hero.CornerRadius = new CornerRadius(width / 2, width / 2, 14, 14);
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimalDetailViewModel viewModel)
            await viewModel.GoBackAsync();
    }
}
