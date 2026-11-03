using System.Text.Json;
using Microsoft.JSInterop;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// The system notifications of a Blazor application: the notification center of the browser. With the service worker
/// (<see cref="AppNotificationOptions.ServiceWorkerUrl"/>) a notification has action buttons and a reply field; without it the page notifies by itself
/// (Notification API) and only the click and the close are reported. The clicks come back as <c>NotificationCompleted</c>: <c>ActionTag</c> is the
/// button, <c>UserData</c> the text of a reply.
/// </summary>
internal sealed class BrowserSystemNotifications(IJSRuntime js, AppNotificationOptions options) : IAsyncDisposable
{
    private const string Module = "./_content/Routeables.Razor/notifications.js";

    private readonly Dictionary<uint, BrowserNotification> _active = [];
    private readonly object _gate = new();
    private Task<IJSObjectReference>? _module;
    private Task? _ready;
    private DotNetObjectReference<Callback>? _callback;
    private uint _lastId;

    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted;

    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
    {
        lock (_gate)
            return _active.ToDictionary(p => p.Key, p => (IRouteableSystemNotification)p.Value);
    }

    public async Task InitializeAsync()
    {
        if (options.RequestPermissionOnInitialize)
            await RequestPermissionAsync();
    }

    public async Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => await RequestPermissionAsync() == "granted" ? new BrowserNotification(this, Interlocked.Increment(ref _lastId), category) : null;

    public void CloseAll()
    {
        uint[] ids;
        lock (_gate) ids = [.. _active.Keys];

        // Prerendering or a lost circuit: there is nothing on screen to close.
        if (_module is { IsCompletedSuccessfully: true })
            _ = _module.Result.InvokeVoidAsync("closeAll").AsTask().ContinueWith(_ => { }, TaskScheduler.Default);
        foreach (var id in ids)
            Completed(id, false, null, null);
    }

    public async ValueTask DisposeAsync()
    {
        if (options.ClearOnAppClose)
            CloseAll();
        if (_module is { IsCompletedSuccessfully: true })
        {
            try { await _module.Result.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
        _callback?.Dispose();
    }

    // "granted", "denied", "default" (not asked yet) or "unsupported" (also when JavaScript cannot be called yet, as while prerendering).
    private async Task<string> RequestPermissionAsync()
    {
        try
        {
            var module = await ReadyAsync();
            var permission = await module.InvokeAsync<string>("permission");
            return permission == "default" ? await module.InvokeAsync<string>("requestPermission") : permission;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or JSDisconnectedException)
        {
            _module = null;
            _ready = null;
            return "unsupported";
        }
    }

    // The module, once the messages of the service worker are listened to and the service worker is registered (or found missing).
    private async Task<IJSObjectReference> ReadyAsync()
    {
        _module ??= js.InvokeAsync<IJSObjectReference>("import", Module).AsTask();
        var module = await _module;
        _ready ??= StartAsync(module);
        await _ready;
        return module;
    }

    private async Task StartAsync(IJSObjectReference module)
    {
        _callback ??= DotNetObjectReference.Create(new Callback(this));
        await module.InvokeVoidAsync("registrations", _callback);
        await module.InvokeAsync<bool>("register", options.ServiceWorkerUrl);
    }

    private async Task ShowAsync(BrowserNotification notification)
    {
        var module = await ReadyAsync();
        lock (_gate) _active[notification.Id] = notification;

        // The actions as flat triplets (caption, tag, icon): nothing but strings crosses the interop.
        var actions = notification.Actions?.SelectMany(a => new[] { a.caption, a.tag, a.Icon ?? string.Empty }).ToArray() ?? [];
        try
        {
            await module.InvokeVoidAsync("show", notification.Id, notification.Title, notification.Message,
                notification.Icon ?? options.DefaultIcon, notification.Tag, notification.Expiration?.TotalMilliseconds ?? 0,
                actions, notification.ReplyActionTag);
        }
        catch
        {
            lock (_gate) _active.Remove(notification.Id);
            throw;
        }
    }

    private async Task CloseAsync(uint id)
    {
        if (_module is not null)
            await (await _module).InvokeVoidAsync("close", id);

        // Closed from here, the browser says nothing: the notification is told as cancelled.
        Completed(id, false, null, null);
    }

    private void Completed(uint id, bool activated, string? action, string? reply)
    {
        bool known;
        lock (_gate) known = _active.Remove(id);
        if (!known)
            return;

        NotificationCompleted?.Invoke(this, new RouteableSystemNotificationEventArgs
        {
            NotificationId = id,
            ActionTag = action,
            UserData = reply,
            IsActivated = activated,
            IsCancelled = !activated
        });
    }

    // What the service worker (or the page) tells: {"type":"HandleNotificationClick","id":3,"tag":"...","action":"open"}.
    private void Completed(string json, bool activated, string? reply)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var id) || !id.TryGetUInt32(out var number))
            return;

        var action = root.TryGetProperty("action", out var tag) && tag.ValueKind == JsonValueKind.String ? tag.GetString() : null;
        Completed(number, activated, action, reply);
    }

    // The part of the object the browser calls back.
    private sealed class Callback(BrowserSystemNotifications owner)
    {
        [JSInvokable]
        public void OnClick(string json) => owner.Completed(json, true, null);

        [JSInvokable]
        public void OnClose(string json) => owner.Completed(json, false, null);

        [JSInvokable]
        public void OnReply(string json, string? reply) => owner.Completed(json, true, reply);
    }

    private sealed class BrowserNotification(BrowserSystemNotifications owner, uint id, string? category) : IRouteableSystemNotification
    {
        public uint Id { get; } = id;
        public string? Category { get; } = category;
        public string? Title { get; set; }
        public string? Tag { get; set; }
        public string? Message { get; set; }
        public TimeSpan? Expiration { get; set; }
        public string? Icon { get; set; }
        public string? ReplyActionTag { get; set; }
        public IReadOnlyList<RouteableSystemNotificationAction>? Actions { get; private set; }

        public void SetActions(IReadOnlyList<RouteableSystemNotificationAction>? actions) => Actions = actions;

        public Task ShowAsync() => owner.ShowAsync(this);

        public Task CloseAsync() => owner.CloseAsync(Id);
    }
}
