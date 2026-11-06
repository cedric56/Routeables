using Routeables.Abstraction;
using Routeables.AspNetCore;
using XAnimals.ViewModels;

namespace XAnimals.Razor.Session;

/// <summary>
/// The view of <see cref="NoteViewModel"/> for the web host, shown modally: the page puts a form on the screen and the note goes to
/// the caller when the view model asks to close. The form is drawn by <c>_Interactions.cshtml</c>.
/// </summary>
[RouteableView(typeof(NoteViewModel))]
public sealed class NoteModal(RouteableInteractions interactions) : IRouteableModalFor<NoteViewModel, string?>
{
    public object? Context { get; set; }

    public async Task<string?> ShowModalAsync()
    {
        var viewModel = (NoteViewModel)Context!;
        var closed = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Subscribe first: a view model that closes right away must not be missed.
        void OnClose(object? sender, string? value) => closed.TrySetResult(value);
        viewModel.CloseRequested += OnClose;

        var dialog = new RouteableDialog(viewModel.Title, null, null, []) { ViewModel = viewModel };
        interactions.Show(dialog);
        try
        {
            return await closed.Task;
        }
        finally
        {
            viewModel.CloseRequested -= OnClose;
            dialog.Answer.TrySetResult(0);
            await interactions.Close(dialog);
        }
    }
}
