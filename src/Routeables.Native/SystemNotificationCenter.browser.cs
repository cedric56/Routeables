#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Routeables.Native;

// The browser, from the plain net10.0 target (a WebAssembly application that is not Blazor: Avalonia, Uno): the notification center of the browser, through the same
// JavaScript module as Routeables.Blazor (wwwroot/notifications.js, embedded here and imported from a data address, so nothing has to be served). With the service
// worker a notification has action buttons and a reply field; without it the page notifies by itself and only the click and the close are reported.
public sealed partial class SystemNotificationCenter
{
    private const string BrowserModule = "routeables-notifications";

    private Task? _browserReady;

    /// <summary>"granted", "denied", "default" (not asked yet) or "unsupported".</summary>
    [SupportedOSPlatform("browser")]
    private async Task<string> BrowserPermissionAsync(bool ask)
    {
        try
        {
            await BrowserReadyAsync();
            var permission = BrowserPermission();
            return ask && permission == "default" ? await BrowserRequestPermission() : permission;
        }
        catch (JSException)
        {
            _browserReady = null;
            return "unsupported";
        }
    }

    [SupportedOSPlatform("browser")]
    private async Task BrowserShowAsync(SystemNotification notification)
    {
        try
        {
            var options = Options ?? new AppNotificationOptions();
            await BrowserReadyAsync();

            // The actions as flat triplets (caption, tag, icon): nothing but strings crosses the interop.
            var actions = notification.Actions?.SelectMany(a => new[] { a.caption, a.tag, a.Icon ?? string.Empty }).ToArray() ?? [];
            await BrowserShow((int)notification.Id, notification.Title, notification.Message, notification.Icon ?? options.DefaultIcon, notification.Tag,
                notification.Expiration?.TotalMilliseconds ?? 0, actions, notification.ReplyActionTag);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceWarning($"Routeables: the browser could not show the notification: {ex.Message}");
            Complete(notification.Id, activated: false, action: null);
        }
    }

    [SupportedOSPlatform("browser")]
    private void BrowserClose(uint id) => _ = BrowserSafe(BrowserCloseOne((int)id));

    [SupportedOSPlatform("browser")]
    private void BrowserCloseAll() => _ = BrowserSafe(BrowserCloseEvery());

    private static async Task BrowserSafe(Task task)
    {
        try { await task; }
        catch (JSException) { }
    }

    [SupportedOSPlatform("browser")]
    private Task BrowserReadyAsync() => _browserReady ??= BrowserStartAsync();

    [SupportedOSPlatform("browser")]
    private async Task BrowserStartAsync()
    {
        using var stream = typeof(SystemNotificationCenter).Assembly.GetManifestResourceStream("Routeables.Native.notifications.js")
            ?? throw new InvalidOperationException("The notification module is not embedded in Routeables.Native.");
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        await JSHost.ImportAsync(BrowserModule, "data:text/javascript;base64," + Convert.ToBase64String(memory.ToArray()));

        await BrowserUseCallbacks(json => BrowserTold(json, true, null), json => BrowserTold(json, false, null), (json, reply) => BrowserTold(json, true, reply));
        await BrowserRegister((Options ?? new AppNotificationOptions()).ServiceWorkerUrl);
    }

    // What the service worker (or the page) tells: {"type":"HandleNotificationClick","id":3,"tag":"...","action":"open"}.
    private void BrowserTold(string json, bool activated, string? reply)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var id) || !id.TryGetUInt32(out var number))
            return;

        var action = root.TryGetProperty("action", out var tag) && tag.ValueKind == JsonValueKind.String ? tag.GetString() : null;
        Complete(number, activated, action, reply);
    }

    [JSImport("permission", BrowserModule)]
    private static partial string BrowserPermission();

    [JSImport("requestPermission", BrowserModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    private static partial Task<string> BrowserRequestPermission();

    [JSImport("register", BrowserModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
    private static partial Task<bool> BrowserRegister(string? url);

    [JSImport("useCallbacks", BrowserModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task BrowserUseCallbacks(
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onClick,
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onClose,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.String>>] Action<string, string?> onReply);

    [JSImport("show", BrowserModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task BrowserShow(int id, string? title, string? body, string? icon, string? tag, double expirationMs,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] actions, string? replyActionTag);

    [JSImport("close", BrowserModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task BrowserCloseOne(int id);

    [JSImport("closeAll", BrowserModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task BrowserCloseEvery();
}
#endif
