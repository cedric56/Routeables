using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// What the dialog and notification services of the web host share with the pages. On a desktop a dialog is a window and the view model
/// awaits it; here the view model awaits too, but the answer comes with a later request: <see cref="StartAsync"/> runs an action until
/// it ends or asks something, and <see cref="ContinueAsync"/> goes on once the visitor has answered.
/// </summary>
public sealed class RouteableInteractions : IDisposable
{
    private readonly List<RouteableDialog> _dialogs = [];
    private readonly List<RouteableToast> _toasts = [];
    private TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The action that is running in the flow of the caller: the dialogs it asks belong to it, so that answering one waits for
    // that action, not for another one the visitor started in the meantime.
    private sealed class Run { public Task? Task; }
    private readonly AsyncLocal<Run?> _current = new();
    private int _nextToast;

    /// <summary>The dialog that waits for an answer, if any.</summary>
    public RouteableDialog? Pending => _dialogs.Count > 0 ? _dialogs[0] : null;

    /// <summary>The toasts currently shown on the page.</summary>
    public IReadOnlyList<RouteableToast> Toasts => _toasts;

    // ---------------------------------------------------------------- dialogs

    /// <summary>Shows <paramref name="dialog"/> and completes with the button that was chosen (its index).</summary>
    public Task<int> AskAsync(RouteableDialog dialog)
    {
        Show(dialog);
        return Close(dialog);
    }

    /// <summary>Puts <paramref name="dialog"/> on the page; <see cref="Close"/> removes it.</summary>
    public void Show(RouteableDialog dialog)
    {
        dialog.Owner ??= _current.Value;
        _dialogs.Add(dialog);
        _asked.TrySetResult();
    }

    /// <summary>Completes when <paramref name="dialog"/> is answered, and removes it.</summary>
    public async Task<int> Close(RouteableDialog dialog)
    {
        try { return await dialog.Answer.Task.ConfigureAwait(false); }
        finally { _dialogs.Remove(dialog); }
    }

    /// <summary>Runs <paramref name="action"/> until it ends or shows a dialog.</summary>
    public Task StartAsync(Func<Task> action)
    {
        _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = new Run();
        var outer = _current.Value;
        _current.Value = run;
        try { run.Task = action(); }
        finally { _current.Value = outer; }   // StartAsync is not async: what it sets would stay in the flow of the caller
        return SettleAsync(run);
    }

    /// <summary>Does <paramref name="answer"/> (what the visitor chose), then waits for the action that was started to end or to ask again.</summary>
    public Task ContinueAsync(Action answer) => ContinueAsync(answer, Pending?.Owner as Run);

    private Task ContinueAsync(Action answer, Run? owner)
    {
        _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        answer();
        return SettleAsync(owner);
    }

    /// <summary>Gives the button <paramref name="index"/> as the answer of the dialog that waits.</summary>
    public Task AnswerAsync(int index)
    {
        var dialog = Pending;
        return ContinueAsync(() => dialog?.Answer.TrySetResult(index), dialog?.Owner as Run);
    }

    // Waits for the action to end or to ask something; its failure reaches the request that waits for it.
    private async Task SettleAsync(Run? run)
    {
        if (run?.Task is not { } task)
            return;

        await Task.WhenAny(task, _asked.Task);
        if (!task.IsCompleted)
            return;

        await task;
    }

    // ---------------------------------------------------------------- notifications

    /// <summary>Adds a toast notification that the page will show.</summary>
    public void Notify(string title, string message, RouteableNotificationType type, TimeSpan? expiration, Action? onClick, Action? onClose)
        => _toasts.Add(new RouteableToast(++_nextToast, title, message, type, expiration, onClick, onClose));

    /// <summary>Drops the notifications whose time is over, as the page they were shown on has been left.</summary>
    public void Expire()
    {
        foreach (var toast in _toasts.Where(t => t.Expiration is { } span && DateTime.UtcNow - t.Shown > span).ToList())
            Dismiss(toast.Id, click: false);
    }

    /// <summary>Removes the toast with the given <paramref name="id"/> and optionally raises its click handler.</summary>
    public void Dismiss(int id, bool click)
    {
        if (_toasts.FirstOrDefault(t => t.Id == id) is not { } toast)
            return;

        _toasts.Remove(toast);
        if (click)
            toast.OnClick?.Invoke();
        toast.OnClose?.Invoke();
    }

    /// <summary>
    /// Cancels any dialogs that are still waiting for an answer (the visitor will never answer them once the session ends).
    /// </summary>
    public void Dispose()
    {
        // A view model that awaits a dialog the visitor will never answer ends with the session.
        foreach (var dialog in _dialogs)
            dialog.Answer.TrySetCanceled();
        _dialogs.Clear();
    }
}