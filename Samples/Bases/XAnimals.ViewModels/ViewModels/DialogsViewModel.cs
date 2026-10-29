using Routeables.Abstraction;
using Routeables.Services;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The Dialogs page: one button for every kind of dialog and notification Routeables offers, and the answer the user gave.
/// The services are the platform's own (Razor, Avalonia, Maui or Uno), so the same view model drives the four apps.
/// </summary>
[Routeable("dialogs")]
public class DialogsViewModel : ViewModelBase
{
    private readonly IRouteableDialogService _dialogs;
    private readonly IRouteableNotificationService _notifications;
    private readonly IServiceProvider _provider;
    private string _result = string.Empty;

    public DialogsViewModel(
        IRouteableDialogService dialogs,
        IRouteableNotificationService notifications,
        IServiceProvider provider)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _provider = provider;

        // The same actions as commands, for the XAML views (the Razor view calls the methods).
        OkCommand = new AsyncCommand(ShowOkAsync);
        ErrorCommand = new AsyncCommand(ShowErrorAsync);
        YesNoCommand = new AsyncCommand(ShowYesNoAsync);
        OkCancelCommand = new AsyncCommand(ShowOkCancelAsync);
        OkAbortCommand = new AsyncCommand(ShowOkAbortAsync);
        YesNoCancelCommand = new AsyncCommand(ShowYesNoCancelAsync);
        YesNoAbortCommand = new AsyncCommand(ShowYesNoAbortAsync);
        ContentDialogCommand = new AsyncCommand(ShowContentDialogAsync);
        TaskDialogCommand = new AsyncCommand(ShowTaskDialogAsync);
        ProgressDialogCommand = new AsyncCommand(ShowProgressDialogAsync);
        ModalCommand = new AsyncCommand(ShowModalAsync);
        InformationCommand = new AsyncCommand(() => NotifyAsync(RouteableNotificationType.Information));
        SuccessCommand = new AsyncCommand(() => NotifyAsync(RouteableNotificationType.Success));
        WarningCommand = new AsyncCommand(() => NotifyAsync(RouteableNotificationType.Warning));
        NotificationErrorCommand = new AsyncCommand(() => NotifyAsync(RouteableNotificationType.Error));
    }

    /// <summary>What the user answered last; empty until a dialog or notification was used.</summary>
    public string Result
    {
        get => _result;
        private set
        {
            if (_result == value) return;
            _result = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasResult));
        }
    }

    public bool HasResult => _result.Length > 0;

    public ICommand OkCommand { get; }
    public ICommand ErrorCommand { get; }
    public ICommand YesNoCommand { get; }
    public ICommand OkCancelCommand { get; }
    public ICommand OkAbortCommand { get; }
    public ICommand YesNoCancelCommand { get; }
    public ICommand YesNoAbortCommand { get; }
    public ICommand ContentDialogCommand { get; }
    public ICommand TaskDialogCommand { get; }
    public ICommand ProgressDialogCommand { get; }
    public ICommand ModalCommand { get; }
    public ICommand InformationCommand { get; }
    public ICommand SuccessCommand { get; }
    public ICommand WarningCommand { get; }
    public ICommand NotificationErrorCommand { get; }

    // ---------------------------------------------------------------- IRouteableDialogService

    public async Task ShowOkAsync()
    {
        await _dialogs.ShowOkAsync("Your favorites were saved.", "Saved");
        Result = "Message closed.";
    }

    public async Task ShowErrorAsync()
    {
        await _dialogs.ShowErrorAsync(new InvalidOperationException("The bear list could not be loaded."));
        Result = "Error closed.";
    }

    public async Task ShowYesNoAsync()
    {
        var yes = await _dialogs.ShowYesNoAsync("Add the polar bear to your favorites?", "Favorites");
        Result = $"Answer: {(yes ? "Yes" : "No")}.";
    }

    public async Task ShowOkCancelAsync()
    {
        var ok = await _dialogs.ShowOkCancelAsync("Clear the search?", "Search");
        Result = $"Answer: {(ok ? "OK" : "Cancel")}.";
    }

    public async Task ShowOkAbortAsync()
    {
        var ok = await _dialogs.ShowOkAbortAsync("Reload all animals?", "Reload");
        Result = $"Answer: {(ok ? "OK" : "Abort")}.";
    }

    // The index is the button: 0 = Yes, 1 = No, 2 = Cancel (or Abort).
    public async Task ShowYesNoCancelAsync()
    {
        var answer = await _dialogs.ShowYesNoCancelAsync("Save your changes before leaving?", "Unsaved changes");
        Result = $"Answer: {Name(answer, "Cancel")}.";
    }

    public async Task ShowYesNoAbortAsync()
    {
        var answer = await _dialogs.ShowYesNoAbortAsync("Replace the existing list?", "Import");
        Result = $"Answer: {Name(answer, "Abort")}.";
    }

    private static string Name(int answer, string third) => answer switch { 0 => "Yes", 1 => "No", 2 => third, _ => "none" };

    // ---------------------------------------------------------------- IRouteableContentDialogService

    public async Task ShowContentDialogAsync()
    {
        var result = await _dialogs.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "Share this animal",
            Content = "Send the link to a friend?",
            PrimaryButtonText = "Send",
            IsPrimaryButtonEnabled = true,
            SecondaryButtonText = "Copy link",
            IsSecondaryButtonEnabled = true,
            CloseButtonText = "Cancel"
        });
        Result = result switch
        {
            RouteableContentDialogResult.Primary => "Chose Send.",
            RouteableContentDialogResult.Secondary => "Chose Copy link.",
            _ => "Closed without a choice."
        };
    }

    // ---------------------------------------------------------------- IRouteableTaskDialogService

    public async Task ShowTaskDialogAsync()
    {
        var result = await _dialogs.ShowTaskDialogAsync(
            new RouteableTaskDialog
            {
                Title = "Photos",
                Header = "Download all animal photos?",
                SubHeader = "About 40 MB",
                Content = "The photos are kept on this device so the lists work offline.",
                Footer = "You can change this later in the settings.",
                FooterVisibility = RouteableTaskDialogFooterVisibility.Auto,
                IsFooterExpanded = false
            },
            RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.No, RouteableTaskDialogResult.Cancel);
        Result = $"Answer: {result}.";
    }

    public async Task ShowProgressDialogAsync()
    {
        var result = await _dialogs.ShowTaskDialogAsync(
            new RouteableTaskDialog
            {
                Title = "Photos",
                Header = "Downloading photos",
                SubHeader = "This can take a minute",
                Content = "Keep the app open until it is done.",
                ShowProgressBar = true
            },
            RouteableTaskDialogResult.Cancel);
        Result = result == RouteableTaskDialogResult.Cancel ? "Download cancelled." : $"Answer: {result}.";
    }

    // ---------------------------------------------------------------- modal

    // A view model with its own view, shown modally by each platform; it returns what the user typed.
    public async Task ShowModalAsync()
    {
        var note = await _provider.ShowModalAsync<NoteViewModel, string>();
        Result = note is null ? "Note cancelled." : $"Note saved: {note}";
    }

    // ---------------------------------------------------------------- IRouteableNotificationService

    private Task NotifyAsync(RouteableNotificationType type)
    {
        var (title, message) = type switch
        {
            RouteableNotificationType.Success => ("Saved", "Your favorites were saved."),
            RouteableNotificationType.Warning => ("Slow connection", "Photos may take longer to appear."),
            RouteableNotificationType.Error => ("Download failed", "The photos could not be downloaded."),
            _ => ("Tip", "Search a list to find an animal faster.")
        };

        _notifications.Show(
            title, message, type,
            expiration: TimeSpan.FromSeconds(6),
            onClick: () => Result = $"Clicked the {type.ToString().ToLowerInvariant()} notification.",
            onClose: () => Result = $"Closed the {type.ToString().ToLowerInvariant()} notification.");
        return Task.CompletedTask;
    }
}
