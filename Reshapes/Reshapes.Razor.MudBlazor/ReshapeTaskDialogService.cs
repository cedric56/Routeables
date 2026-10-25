using MudBlazor;
using Reshapes.Razor.MudBlazor.Components;
using Routeables.Abstraction;
using Routeables.Razor.Components;
using Routeables.Services;

namespace Reshapes.Razor.MudBlazor;

/// <summary>
/// Provides a concrete implementation of <see cref="IRouteableTaskDialogService"/> using MudBlazor's dialog service 
/// to display task dialogs with customizable headers, buttons, and standard results asynchronously.
/// </summary>
/// <param name="MudDialogService">The underlying MudBlazor dialog service.</param>
internal class ReshapeTaskDialogService(IDialogService MudDialogService) : IRouteableTaskDialogService
{
    /// <summary>
    /// Displays a task dialog asynchronously with the specified parameters and available result choices.
    /// </summary>
    /// <param name="params">The parameters governing the task dialog's configuration, text, icon, and content.</param>
    /// <param name="results">An array of standard results representing the action buttons to present to the user.</param>
    /// <returns>
    /// A task that completes with the <see cref="RouteableTaskDialogResult"/> corresponding to the button selected by the user.
    /// </returns>
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        var dialog = await MudDialogService.ShowAsync<TaskDialog>(@params.Title,
        new DialogParameters()
        {
            { nameof(TaskDialog.Parameters), @params },
            { nameof(TaskDialog.Results), results }
        },
        new DialogOptions() { BackdropClick = true });

        var result = await dialog.Result;
        return result?.Data is RouteableTaskDialogResult cdr ? cdr : RouteableTaskDialogResult.None;
    }
}