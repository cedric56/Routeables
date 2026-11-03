using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// Draws the dialogs with plain Razor, without any UI library: message boxes, content dialogs and task dialogs are queued and shown
/// one at a time by the hosts (<c>RouteableDialogHost</c>, <c>RouteableContentDialogHost</c>, <c>RouteableTaskDialogHost</c>) placed once
/// in the layout of the application. It is the <see cref="IMessageBoxService"/> of <c>UseRouteablesServices</c> unless another one is given.
/// </summary>
public sealed class DefaultMessageBoxService : IMessageBoxService
{
    private readonly RouteableMessageQueue _messages;
    private readonly RouteableContentDialogQueue _content;
    private readonly RouteableTaskDialogQueue _task;

    internal DefaultMessageBoxService(RouteableMessageQueue messages, RouteableContentDialogQueue content, RouteableTaskDialogQueue task)
    {
        _messages = messages;
        _content = content;
        _task = task;
    }

    /// <inheritdoc />
    public Task<int> ShowMessageAsync(string title, string message, string buttons)
    {
        var words = RouteableButtons.Split(buttons);
        if (words.Count == 0)
            words = ["Ok"];

        // The first button is the default one (index 0): it is drawn last, on the right, as the primary one.
        var drawn = new DialogButton[words.Count];
        for (var i = 0; i < words.Count; i++)
            drawn[words.Count - 1 - i] = new DialogButton(Label(words[i]), i, IsPrimary: i == 0);

        // Escape or a click on the backdrop answers with the last button: Cancel, Abort or No, and the only button of an information.
        return _messages.ShowAsync(title, message, words.Count - 1, drawn);
    }

    private static string Label(string word) => word == "Ok" ? "OK" : word;

    /// <inheritdoc />
    public Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
        => _content.ShowAsync(@params);

    /// <inheritdoc />
    public Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
        => _task.ShowAsync(@params, results);

    /// <summary>A modal is shown by its own component, <c>RouteableModalFor&lt;TView, TViewModel, TResult&gt;.ShowModalAsync()</c>.</summary>
    /// <exception cref="NotSupportedException">Always: a view model alone does not say which component draws it.</exception>
    public Task<TResult?> ShowModalAsync<TViewModel, TResult>(TViewModel viewModel)
        where TViewModel : ICloseableViewModel<TResult>
        => throw new NotSupportedException(
            "A modal needs its component: derive it from RouteableModalFor<TView, TViewModel, TResult> and call ShowModalAsync() on it.");
}
