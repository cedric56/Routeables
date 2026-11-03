using Microsoft.AspNetCore.Components;
using Routeables.Abstraction;
using Routeables.Razor;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Razor.Services;

/// <summary>
/// Shows a view and its view model in a modal dialog and returns a strongly typed result when the view model
/// asks to close. The modal is rendered by a <see cref="RouteableModalHost"/> placed once in the layout.
/// </summary>
/// <typeparam name="TView">The Blazor component type representing the view.</typeparam>
/// <typeparam name="TViewModel">The view model type controlling the modal and managing closure events.</typeparam>
/// <typeparam name="TResult">The type of result value produced when the modal closes.</typeparam>
public partial class RouteableModalFor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TView, TViewModel, TResult>()
    : RouteableFor<TView, TViewModel>, IRouteableModalFor<TViewModel, TResult>
    where TViewModel : class, ICloseableViewModel<TResult>
    where TView : ComponentBase
{
    /// <summary>The service that keeps the open modals.</summary>
    [Inject]
    internal RouteableModalService Modals { get; set; } = default!;

    /// <summary>
    /// Displays the modal dialog asynchronously and awaits its close result.
    /// </summary>
    /// <returns>
    /// A task that completes with the <typeparamref name="TResult"/> returned by the view model.
    /// </returns>
    /// <exception cref="InvalidOperationException">No <see cref="RouteableModalHost"/> is displayed.</exception>
    public async Task<TResult?> ShowModalAsync()
    {
        if (Modals == null)
            return default;

        var viewModel = ViewModel;
        var tcs = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Subscribe first: a view model that closes right away must not be missed.
        void CloseRequestedHandler(object? sender, TResult? e) => tcs.TrySetResult(e);
        viewModel.CloseRequested += CloseRequestedHandler;

        ModalRequest? modal = null;
        try
        {
            modal = Modals.Show(viewModel.Title, Render()!);
            return await tcs.Task;
        }
        finally
        {
            viewModel.CloseRequested -= CloseRequestedHandler;
            if (modal is not null) Modals.Close(modal);
        }
    }


}