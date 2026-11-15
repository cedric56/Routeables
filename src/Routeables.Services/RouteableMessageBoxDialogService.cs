namespace Routeables.Services;

/// <summary>
/// The <see cref="IRouteableDialogService"/> of a host that draws its dialogs itself (WPF, Avalonia, WinUI and Uno), through its own message box service:
/// what the buttons of a message mean, the index that is returned, and the dialogs that are asked for with nothing are the same everywhere. A host gives only
/// how to go to its interface thread and how to show each kind of dialog.
/// <para>The index returned follows the order of the buttons (Yes, No, then Cancel or Abort); -1 when the dialog was dismissed.</para>
/// </summary>
public abstract class RouteableMessageBoxDialogService : RouteableDialogServiceBase
{
    /// <summary>Runs <paramref name="action"/> on the interface thread, at once when already there, and gives its result.</summary>
    protected abstract Task<T> RunOnInterfaceThreadAsync<T>(Func<Task<T>> action);

    /// <summary>Shows a message with the buttons of <paramref name="buttonText"/> and gives the text of the button pressed; <see langword="null"/> when dismissed.</summary>
    protected abstract Task<string?> ShowMessageBoxAsync(string title, string message, string buttonText);

    /// <summary>Shows a content dialog, on the interface thread.</summary>
    protected abstract Task<RouteableContentDialogResult> ShowContentAsync(RouteableContentDialog @params);

    /// <summary>Shows a task dialog, on the interface thread.</summary>
    protected abstract Task<RouteableTaskDialogResult> ShowTaskAsync(RouteableTaskDialog @params, RouteableTaskDialogResult[] results);

    /// <inheritdoc />
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
        => RunOnInterfaceThreadAsync(async () =>
        {
            var words = RouteableButtons.Split(buttonText);
            var result = await ShowMessageBoxAsync(title, message, buttonText).ConfigureAwait(true);
            return result is not null ? IndexOf(words, result) : -1;
        });

    /// <inheritdoc />
    public override Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);
        return RunOnInterfaceThreadAsync(() => ShowContentAsync(@params));
    }

    /// <inheritdoc />
    public override Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        if (@params is null)
            return Task.FromResult(RouteableTaskDialogResult.None);

        return RunOnInterfaceThreadAsync(() => ShowTaskAsync(@params, results));
    }

    private static int IndexOf(IReadOnlyList<string> words, string word)
    {
        for (var i = 0; i < words.Count; i++)
            if (words[i] == word)
                return i;
        return -1;
    }
}
