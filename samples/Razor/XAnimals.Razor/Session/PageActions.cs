using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Routing;
using Routeables.AspNetCore;
using Routeables.Services;
using XAnimals.Common;
using XAnimals.ViewModels;
using IRouter = Routeables.Abstraction.Routing.IRouter;

namespace XAnimals.Razor.Session;

/// <summary>
/// What a button of a page can ask of a view model. A page is HTML: a button posts the name of the view model's region
/// (<c>page</c> for the main router), the name of an action and, after a colon, an index. Only the actions listed here exist; nothing is
/// found by reflection from what the browser sends.
/// </summary>
public static class PageActions
{
    /// <summary>The target of the buttons of the dialog that waits for an answer.</summary>
    public const string Dialog = "dialog";

    /// <summary>The target of the buttons of the notifications.</summary>
    public const string Toast = "toast";

    /// <summary>The target of the report of a system notification of the browser (clicked or closed).</summary>
    public const string System = "system";

    /// <summary>The target of the button of the theme.</summary>
    public const string Theme = "theme";

    /// <summary>The target of the buttons of the page the main router shows.</summary>
    public const string Page = "page";

    /// <summary>The tabs of the Tabs page, as the other platforms declare them: the route each one shows in the region of the page.</summary>
    public static readonly IReadOnlyList<(string Header, string Route)> TabRoutes = [("Cats", "cats"), ("Dogs", "dogs"), ("Monkeys", "monkeys")];

    /// <summary>What a page does when it appears: the Tabs page shows its first tab, as a tab control selects its first item.</summary>
    public static async Task ShownAsync(IServiceProvider provider, object? viewModel)
    {
        if (viewModel is TabsViewModel)
            await provider.GetRouter(TabsViewModel.Region).GoToAsync(TabRoutes[0].Route);
    }

    public static async Task RunAsync(IServiceProvider provider, string target, string action, string? value)
    {
        var (name, index) = Split(action);
        var interactions = provider.GetRequiredService<RouteableInteractions>();

        switch (target)
        {
            case Dialog:
                await RunDialogAsync(interactions, name, index, value);
                return;
            case Toast:
                interactions.Dismiss(index, click: name == "click");
                return;
            case System:
                interactions.CompleteSystemNotification((uint)index, activated: name == "click");
                return;
            case Theme:
                // Default (the browser decides), then light, then dark.
                var themes = provider.GetRequiredService<RouteableRequestThemeService>();
                themes.Request(themes.Requested switch
                {
                    RouteableRequestedTheme.Default => RouteableRequestedTheme.Light,
                    RouteableRequestedTheme.Light => RouteableRequestedTheme.Dark,
                    _ => RouteableRequestedTheme.Default
                });
                return;
        }

        var router = target == Page ? provider.GetRequiredService<IRouter>() : provider.GetRouter(target);
        switch (router.CurrentViewModel)
        {
            case HistoryViewModel history:
                await RunHistoryAsync(history, name, value);
                break;
            case RegionsViewModel regions:
                await RunRegionsAsync(regions, name);
                break;
            case LayoutMenuViewModel:
                await RunMenuAsync(provider, name, index);
                break;
            case AdoptViewModel adopt:
                await RunAdoptAsync(adopt, name, index);
                break;
            case DialogsViewModel dialogs:
                await RunDialogsAsync(interactions, dialogs, name);
                break;
            case FeaturesViewModel features:
                await RunFeaturesAsync(features, name);
                break;
            case TabsViewModel when name == "tab" && index >= 0 && index < TabRoutes.Count:
                await provider.GetRouter(TabsViewModel.Region).GoToAsync(TabRoutes[index].Route);
                break;
            case AnimalsViewModel animals when name == "open" && index >= 0 && index < animals.FilteredAnimals.Count:
                await animals.OnSelectedAnimalAsync(animals.FilteredAnimals[index]);
                break;
            case AnimalDetailViewModel detail when name == "back":
                await detail.GoBackAsync();
                break;
            case MembersViewModel members when name == "signout":
                await members.SignOutAsync();
                break;
            default:
                throw new InvalidOperationException($"The page in '{target}' ({router.CurrentViewModel?.GetType().Name ?? "none"}) has no action '{action}'.");
        }
    }

    private static (string Name, int Index) Split(string action)
    {
        var colon = action.IndexOf(':');
        return colon < 0
            ? (action, 0)
            : (action[..colon], int.TryParse(action[(colon + 1)..], out var index) ? index : 0);
    }

    private static async Task RunHistoryAsync(HistoryViewModel history, string name, string? value)
    {
        switch (name)
        {
            case "push": await history.PushAsync(); break;
            case "back": await history.BackAsync(); break;
            case "forward": await history.ForwardAsync(); break;
            case "pop": await history.PopAsync(); break;
            case "block": history.BlockLeaving = !history.BlockLeaving; break;
            case "transition": history.Transition = value ?? "None"; break;
        }
    }

    private static async Task RunRegionsAsync(RegionsViewModel regions, string name)
    {
        switch (name)
        {
            case "credits": await regions.ToggleCreditsAsync(); break;
            case "left": regions.ToggleLeft(); break;
        }
    }

    // The menu of the left panel is the one of the layout: the same object the view model drives.
    private static async Task RunMenuAsync(IServiceProvider provider, string name, int index)
    {
        var layout = provider.GetRequiredService<LayoutController>();
        switch (name)
        {
            case "back": await layout.BackAsync(); break;
            case "forward": await layout.ForwardAsync(); break;
            case "select" when index >= 0 && index < layout.Pages.Count: await layout.ShowPageAsync(layout.Pages[index]); break;
        }
    }

    private static async Task RunAdoptAsync(AdoptViewModel adopt, string name, int index)
    {
        switch (name)
        {
            case "adopt" when index >= 0 && index < adopt.Choices.Count: await ((AsyncCommand)adopt.Choices[index].AdoptCommand).ExecuteAsync(); break;
            case "release": await adopt.ReleaseAsync(); break;
            case "releaseall": await adopt.ReleaseAllAsync(); break;
        }
    }

    private static async Task RunFeaturesAsync(FeaturesViewModel features, string name)
    {
        switch (name)
        {
            case "signin": await features.SignInAsync(); break;
            case "signout": await features.SignOutAsync(); break;
            case "members": await features.OpenMembersAsync(); break;
            case "link": await features.OpenLinkAsync(); break;
            case "lock": await features.LockAsync(); break;
            case "unlock": await features.UnlockAsync(); break;
            case "refresh": await features.RefreshAsync(); break;
            case "lockfor":
                // The countdown goes on after the page is sent (the menu opens again by itself): it is not awaited.
                _ = features.LockForAsync();
                break;
        }
    }

    // A dialog waits for its answer while the request returns: the view model is started, and goes on when the answer comes.
    private static Task RunDialogsAsync(RouteableInteractions interactions, DialogsViewModel d, string name)
    {
        Func<Task>? action = name switch
        {
            "ok" => d.ShowOkAsync,
            "error" => d.ShowErrorAsync,
            "yesno" => d.ShowYesNoAsync,
            "okcancel" => d.ShowOkCancelAsync,
            "okabort" => d.ShowOkAbortAsync,
            "yesnocancel" => d.ShowYesNoCancelAsync,
            "yesnoabort" => d.ShowYesNoAbortAsync,
            "content" => d.ShowContentDialogAsync,
            "task" => d.ShowTaskDialogAsync,
            "progress" => d.ShowProgressDialogAsync,
            "modal" => d.ShowModalAsync,
            "information" => () => ((AsyncCommand)d.InformationCommand).ExecuteAsync(),
            "success" => () => ((AsyncCommand)d.SuccessCommand).ExecuteAsync(),
            "warning" => () => ((AsyncCommand)d.WarningCommand).ExecuteAsync(),
            "notificationerror" => () => ((AsyncCommand)d.NotificationErrorCommand).ExecuteAsync(),
            "system" => () => ((AsyncCommand)d.SystemNotificationCommand).ExecuteAsync(),
            _ => null
        };

        return action is null ? Task.CompletedTask : interactions.StartAsync(action);
    }

    private static Task RunDialogAsync(RouteableInteractions interactions, string name, int index, string? value)
    {
        switch (name)
        {
            case "answer":
                return interactions.AnswerAsync(index);

            case "note" when interactions.Pending?.ViewModel is NoteViewModel note:
                // Save is the first command of the note, Cancel the second; an empty note cannot be saved.
                if (index == 0 && string.IsNullOrWhiteSpace(value))
                    return Task.CompletedTask;
                if (index == 0)
                    note.Note = value!;
                var command = (AsyncCommand)note.Commands[index == 0 ? 0 : 1].Command;
                return interactions.ContinueAsync(() => command.Execute(null));

            default:
                return Task.CompletedTask;
        }
    }
}