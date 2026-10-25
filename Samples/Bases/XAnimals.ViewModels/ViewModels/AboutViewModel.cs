using Routeables.Abstraction;
using XAminals.Common;
using System.Windows.Input;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The About page: the logo, a link to the project and a shortcut to the polar bears. The dialogs and notifications are on the
/// Dialogs page (<see cref="DialogsViewModel"/>).
/// </summary>
[Routeable("about")]
public class AboutViewModel : ViewModelBase
{
    public const string LearnMoreUrl = "https://github.com/cedric56/Routeables";

    private readonly IMainViewModel _main;

    public AboutViewModel(IMainViewModel main)
    {
        _main = main;

        // The same action as a command, for the XAML views (the Razor view calls the method).
        PolarBearsCommand = new AsyncCommand(OpenPolarBearsAsync);
    }

    public ICommand PolarBearsCommand { get; }

    // Navigation by segments: the Bears list, searching for "polar".
    public async Task OpenPolarBearsAsync()
    {
        if (_main is MainViewModel main)
            await main.GoToAsync("/Bears?SearchText=polar");
    }
}
