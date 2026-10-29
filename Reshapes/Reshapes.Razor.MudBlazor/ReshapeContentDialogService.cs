//using MudBlazor;
//using Reshapes.Razor.MudBlazor.Components;
//using Routeables.Abstraction;
//using Routeables.Services;

//namespace Reshapes.Razor.MudBlazor;

///// <summary>
///// Provides a concrete implementation of <see cref="IRouteableContentDialogService"/> using MudBlazor's dialog service 
///// to display and manage custom content dialogs asynchronously.
///// </summary>
///// <param name="MudDialogService">The underlying MudBlazor dialog service.</param>
//internal class ReshapeContentDialogService(IDialogService MudDialogService) : IRouteableContentDialogService
//{
//    /// <summary>
//    /// Displays a content dialog asynchronously with the specified parameters.
//    /// </summary>
//    /// <param name="params">The parameters governing the content dialog's configuration, content, and events.</param>
//    /// <returns>
//    /// A task that completes with the <see cref="RouteableContentDialogResult"/> indicating how the user closed the dialog.
//    /// </returns>
//    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
//    {
//        var dialog = await MudDialogService.ShowAsync<ContentDialog>(@params.Title,
//        new DialogParameters() { { nameof(ContentDialog.Parameters), @params } },
//        new DialogOptions() { BackdropClick = true });

//        var result = await dialog.Result;

//        return result?.Data is RouteableContentDialogResult cdr ? cdr : RouteableContentDialogResult.None;
//    }
//}