using Avalonia.Controls;
using Routeables.Abstraction;
using Routeables.Avalonia;
using System;
using XAnimals.ViewModels;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace XAnimals.Avalonia;

public partial class AboutView : RouteableFor<AboutViewModel>
{
    public AboutView()
    {
        InitializeComponent();
    }

    private async void OnLearnMoreClick(object? sender, RoutedEventArgs e)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is not null)
            await launcher.LaunchUriAsync(new Uri(AboutViewModel.LearnMoreUrl));
    }
}
