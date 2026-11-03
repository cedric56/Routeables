using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

/// <summary>The detail of any animal kind; the view that contains it provides the view model as BindingContext.</summary>
public partial class AnimalDetail : ContentView
{
    // Same motion tokens as the Razor and Avalonia samples.
    private const uint Slow = 520, Base = 380;

    public AnimalDetail()
    {
        InitializeComponent();
        // Start hidden so there is no flash before the entrance runs.
        hero.Opacity = 0.3;
        hero.TranslationY = 28;
        text.Opacity = 0;
        text.TranslationY = 10;
        Loaded += async (_, _) => await PlayEntranceAsync();
        SizeChanged += (_, _) => FitHero();
    }

    // The arch is 260 x 330 where there is room. On a phone it must leave the text in sight: at most 55 % of the width and
    // 40 % of the height, same proportions, corners kept round.
    private void FitHero()
    {
        if (Width <= 0 || Height <= 0) return;
        const double FullWidth = 260, Ratio = 330d / 260;
        var width = Math.Min(FullWidth, Width * 0.55);
        if (width * Ratio > Height * 0.4) width = Height * 0.4 / Ratio;
        hero.WidthRequest = width;
        hero.HeightRequest = width * Ratio;
        if (hero.StrokeShape is Microsoft.Maui.Controls.Shapes.RoundRectangle shape)
            shape.CornerRadius = new CornerRadius(width / 2, width / 2, 14, 14);
    }

    private async Task PlayEntranceAsync()
    {
        var arch = Task.WhenAll(
            hero.FadeToAsync(1, Slow, Easing.CubicOut),
            hero.TranslateToAsync(0, 0, Slow, Easing.CubicOut));

        await Task.Delay(140);
        await Task.WhenAll(
            text.FadeToAsync(1, Base, Easing.CubicOut),
            text.TranslateToAsync(0, 0, Base, Easing.CubicOut),
            arch);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try
        {
            if (BindingContext is AnimalDetailViewModel viewModel)
                await viewModel.GoBackAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}");
        }
    }
}
