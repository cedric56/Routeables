using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// What the dialog and notification services of the web host share with the pages. On a desktop a dialog is a window and the view model
/// awaits it; here the view model awaits too, but the answer comes with a later request: <see cref="StartAsync"/> runs an action until
/// it ends or asks something, and <see cref="ContinueAsync(Action)"/> goes on once the visitor has answered.
/// An instance belongs to one visitor, who runs one flow at a time: two requests of the same visitor that start or answer at the same moment
/// (two tabs of the browser, a double click) each wait for their own action: the signal that says "the action asked something" belongs to the action that asks.
/// The lists it keeps (dialogs, toasts, system notifications) are safe to use from such requests: they are changed under a lock, and
/// no callback of the application runs while it is held.
/// </summary>
public sealed class RouteableInteractions : IDisposable
{
    private readonly object _gate = new();
    private readonly List<RouteableDialog> _dialogs = [];
    private readonly List<RouteableToast> _toasts = [];

    // The action running in the caller's flow. The dialogs it asks belong to it, so that answering one waits for that action and not for
    // another action the visitor started meanwhile.
    // The signal "the action asked something" is its own: another request from the visitor must neither be woken by it nor wake this one.
    private sealed class Run
    {
        public Task? Task;
        public volatile TaskCompletionSource Asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly AsyncLocal<Run?> _current = new();
    private int _nextToast;
    private readonly Dictionary<uint, RouteableBrowserNotification> _systemActive = [];
    private readonly List<RouteableBrowserNotificationCommand> _systemCommands = [];
    private uint _nextSystem;

    /// <summary>The dialog that waits for an answer, if any.</summary>
    public RouteableDialog? Pending { get { lock (_gate) return _dialogs.Count > 0 ? _dialogs[0] : null; } }

    /// <summary>The toasts currently shown on the page (a copy: it does not change while the page reads it).</summary>
    public IReadOnlyList<RouteableToast> Toasts { get { lock (_gate) return _toasts.ToArray(); } }


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
        lock (_gate)
            _dialogs.Add(dialog);
        (dialog.Owner as Run)?.Asked.TrySetResult();
    }

    /// <summary>Completes when <paramref name="dialog"/> is answered, and removes it.</summary>
    public async Task<int> Close(RouteableDialog dialog)
    {
        try { return await dialog.Answer.Task.ConfigureAwait(false); }
        finally { lock (_gate) _dialogs.Remove(dialog); }
    }

    /// <summary>Runs <paramref name="action"/> until it ends or shows a dialog.</summary>
    public Task StartAsync(Func<Task> action)
    {
        var run = new Run();
        var outer = _current.Value;
        _current.Value = run;
        try { run.Task = action(); }
        finally { _current.Value = outer; } // StartAsync is not async: what it sets would stay in the caller's flow
        return SettleAsync(run);
    }

    /// <summary>Does <paramref name="answer"/> (what the visitor chose), then waits for the action that was started to end or to ask again.</summary>
    public Task ContinueAsync(Action answer) => ContinueAsync(answer, Pending?.Owner as Run);

    private Task ContinueAsync(Action answer, Run? owner)
    {
        if (owner is not null)
            owner.Asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        answer();
        return SettleAsync(owner);
    }

    /// <summary>
    /// Gives the button <paramref name="index"/> as the answer of the dialog that waits. It answers whichever dialog waits when the request
    /// arrives: a double click on the button of a dialog can answer the one that the first click made appear. Prefer
    /// <see cref="AnswerAsync(int, long)"/>, which names the dialog.
    /// </summary>
    public Task AnswerAsync(int index) => AnswerAsync(Pending, index);

    /// <summary>
    /// Gives the button <paramref name="index"/> as the answer of the dialog <paramref name="dialogId"/> (<see cref="RouteableDialog.Id"/>, sent back by the
    /// form that showed it), if it is the one that waits and nobody answered it yet; an answer to another dialog (a double click, another tab of
    /// the browser) is ignored.
    /// </summary>
    public Task AnswerAsync(int index, long dialogId)
        => Pending is { } dialog && dialog.Id == dialogId ? AnswerAsync(dialog, index) : Task.CompletedTask;

    private Task AnswerAsync(RouteableDialog? dialog, int index)
    {
        // The index comes from the page's form, so anyone can post another number. A button that does not exist is not an answer: the dialog
        // stays, and the action waiting for it must not be told to go on (nor the signal replaced: the request would wait forever).
        if (dialog is not null && (index < 0 || index >= dialog.Buttons.Count))
            return Task.CompletedTask;
        if (dialog is not null && !dialog.TryClaim())
            return Task.CompletedTask; // answered by a request that is still being served

        return ContinueAsync(() => dialog?.Answer.TrySetResult(index), dialog?.Owner as Run);
    }

    /// <summary>The same as <see cref="ContinueAsync(Action)"/> for the dialog <paramref name="dialogId"/> only: nothing is done when another one waits.</summary>
    public Task ContinueAsync(Action answer, long dialogId)
        => Pending is { } dialog && dialog.Id == dialogId && dialog.TryClaim() ? ContinueAsync(answer, dialog.Owner as Run) : Task.CompletedTask;

    // Wait for the action to end or to ask something; its failure reaches the request that waits for it.
    private async Task SettleAsync(Run? run)
    {
        if (run?.Task is not { } task)
            return;

        await Task.WhenAny(task, run.Asked.Task);
        if (!task.IsCompleted)
            return;

        await task;
    }


    /// <summary>Adds a toast notification that the page will show.</summary>
    public void Notify(string title, string message, RouteableNotificationType type, TimeSpan? expiration, Action? onClick, Action? onClose)
    {
        int? evicted = null;
        lock (_gate)
        {
            _toasts.Add(new RouteableToast(++_nextToast, title, message, type, expiration, onClick, onClose));
            if (_toasts.Count > MaxToasts)
                evicted = _toasts[0].Id;
        }

        // A toast that is never closed and never expires would otherwise stay for the whole session of the visitor: the oldest one goes first,
        // and closes as if it had expired.
        if (evicted is { } id)
            Dismiss(id, click: false);
    }

    /// <summary>The most toasts one visitor keeps at the same time.</summary>
    internal const int MaxToasts = 50;


    /// <summary>The system notifications that were shown and not closed, by id.</summary>
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveSystemNotifications
    {
        get { lock (_gate) return _systemActive.ToDictionary(p => p.Key, p => (IRouteableSystemNotification)p.Value); }
    }

    /// <summary>Creates a system notification of the browser, not shown until <see cref="IRouteableSystemNotification.ShowAsync"/>.</summary>
    public RouteableBrowserNotification CreateSystemNotification(string? category)
    {
        lock (_gate)
            return new(this, ++_nextSystem, category);
    }

    internal void ShowSystemNotification(RouteableBrowserNotification notification)
    {
        lock (_gate)
        {
            _systemActive[notification.Id] = notification;
            _systemCommands.RemoveAll(c => c.Op == "show" && c.Id == notification.Id);
            _systemCommands.Add(new RouteableBrowserNotificationCommand(
                "show", notification.Id, notification.Title, notification.Message, notification.Icon, notification.Tag,
                notification.Expiration is { } span ? (int)Math.Min(span.TotalMilliseconds, int.MaxValue) : null));
        }
    }

    internal void CloseSystemNotification(uint id)
    {
        lock (_gate)
        {
            if (!_systemActive.Remove(id))
                return;
            // One that has not reached the browser yet never will.
            if (_systemCommands.RemoveAll(c => c.Op == "show" && c.Id == id) == 0)
                _systemCommands.Add(new RouteableBrowserNotificationCommand("close", id));
        }
    }

    /// <summary>Raised when the visitor clicked a system notification of the browser or closed it (<see cref="CompleteSystemNotification"/>).</summary>
    public event EventHandler<RouteableSystemNotificationEventArgs>? SystemNotificationCompleted;

    /// <summary>
    /// What the page tells the server once the visitor clicked a system notification (<paramref name="activated"/>) or closed it: the
    /// notification is no longer active and <see cref="SystemNotificationCompleted"/> is raised. A notification that is not active (already
    /// reported, or closed by the server) is ignored, so a report that arrives twice raises one event.
    /// </summary>
    /// <returns><see langword="true"/> when the notification was active.</returns>
    public bool CompleteSystemNotification(uint id, bool activated, string? actionTag = null)
    {
        lock (_gate)
        {
            if (!_systemActive.Remove(id))
                return false;
        }

        SystemNotificationCompleted?.Invoke(this, new RouteableSystemNotificationEventArgs
        {
            NotificationId = id,
            ActionTag = actionTag,
            IsActivated = activated,
            IsCancelled = !activated
        });
        return true;
    }

    /// <summary>Closes every system notification that is active.</summary>
    public void CloseAllSystemNotifications()
    {
        lock (_gate)
        {
            _systemActive.Clear();
            _systemCommands.Clear();
            _systemCommands.Add(new RouteableBrowserNotificationCommand("closeAll", 0));
        }
    }

    /// <summary>
    /// What the page being rendered must hand to the browser, as JSON for a <c>&lt;script type="application/json"&gt;</c> (the characters that
    /// could close the tag are escaped); <see langword="null"/> when there is nothing. What it returns is not returned again.
    /// </summary>
    public string? TakeSystemNotificationsJson()
    {
        RouteableBrowserNotificationCommand[] commands;
        lock (_gate)
        {
            if (_systemCommands.Count == 0)
                return null;
            commands = [.. _systemCommands];
            _systemCommands.Clear();
        }

        return System.Text.Json.JsonSerializer.Serialize(commands, CommandsJson);
    }

    // One instance for the process: a new options object per call would rebuild its own metadata cache.
    private static readonly System.Text.Json.JsonSerializerOptions CommandsJson = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Drops the notifications whose time is over, as the page they were shown on has been left.</summary>
    public void Expire()
    {
        RouteableToast[] expired;
        lock (_gate)
            expired = [.. _toasts.Where(t => t.Expiration is { } span && DateTime.UtcNow - t.Shown > span)];
        foreach (var toast in expired)
            Dismiss(toast.Id, click: false);
    }

    /// <summary>Removes the toast with the given <paramref name="id"/> and optionally raises its click handler.</summary>
    public void Dismiss(int id, bool click)
    {
        RouteableToast? toast;
        lock (_gate)
        {
            toast = _toasts.FirstOrDefault(t => t.Id == id);
            if (toast is null || !_toasts.Remove(toast))
                return; // another request took it first: its handlers run once
        }

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
        RouteableDialog[] waiting;
        lock (_gate)
        {
            waiting = [.. _dialogs];
            _dialogs.Clear();
        }

        foreach (var dialog in waiting)
            dialog.Answer.TrySetCanceled();
    }
}