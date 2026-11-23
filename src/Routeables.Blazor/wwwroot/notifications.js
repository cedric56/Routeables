// The browser's notification center for Routeables (the system notifications of IRouteableNotificationService). Used by Routeables.Blazor
// (through IJSRuntime) and by Routeables.Native in WebAssembly (Avalonia, Uno; through JSImport, see useCallbacks).
// With a service worker (routeables-notifications-worker.js, served from the root of the site) a notification can have action buttons and a
// reply field, and outlives the page. Without one the page uses the Notification API and only the click and the close are reported.
let registration = null;
let callback = null;
let registered = false;
let listening = false;
const direct = new Map();

// Every window of the site shares the service worker, and each page numbers its notifications from 1. This value tells this page's
// notifications from another tab's (it is in each notification's data, in the worker's messages and in the default tag).
const owner = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;

// Messages from the service worker (and from the page itself) to .NET: a click, a close or a reply.
export async function registrations(dotNetRef) {
    callback = dotNetRef;
    // One listener, however many times this is called: the latest callback is the one that is told.
    if (!('serviceWorker' in navigator) || listening) return;
    listening = true;
    navigator.serviceWorker.addEventListener('message', function (event) {
        const data = event.data;
        // The worker tells every window, and another tab's notification is not for this page. A worker from before the owner was added does not say who owns it: treat it as before.
        if (!data || (data.owner != null && data.owner !== owner)) return;
        // A click or a reply on this page's notification: tell the worker which window to bring to the front.
        if (data.owner === owner && data.type !== 'HandleNotificationClose')
            (navigator.serviceWorker.controller ?? registration?.active)?.postMessage({ type: 'RouteablesClaim', owner });
        if (!callback) return;
        if (data.type === 'HandleNotificationClose') {
            callback.invokeMethodAsync('OnClose', JSON.stringify(data));
        }
        if (data.type === 'HandleNotificationClick') {
            callback.invokeMethodAsync('OnClick', JSON.stringify(data));
        }
        if (data.type === 'HandleNotificationReply') {
            callback.invokeMethodAsync('OnReply', JSON.stringify(data), data.reply);
        }
    });
}

export function permission() {
    return 'Notification' in window ? Notification.permission : 'unsupported';
}

export async function requestPermission() {
    if (!('Notification' in window)) return 'unsupported';
    return await Notification.requestPermission();
}

// Registers the service worker once. Returns false when there is none (no support, not served, not a secure page): the page then notifies by itself.
export async function register(url) {
    if (registered) return registration !== null;
    registered = true;
    if (!url || !('serviceWorker' in navigator)) return false;
    try {
        // The scope is the application's base address (<base href>): '/' for a site at the root, '/app/' for one hosted below it.
        await navigator.serviceWorker.register(url, { scope: new URL('./', document.baseURI).pathname });
        registration = await navigator.serviceWorker.ready;
        return true;
    } catch {
        registration = null;
        return false;
    }
}

// actions: flat triplets [caption, tag, icon, caption, tag, icon...]; the action named replyActionTag asks for a text.
export async function show(id, title, body, icon, tag, expirationMs, actions, replyActionTag) {
    if (registration) {
        const options = { data: { id, owner }, tag: tag ?? `routeables-${owner}-${id}` };
        if (body) options.body = body;
        if (icon) options.icon = icon;
        if (actions && actions.length > 0) {
            options.actions = [];
            for (let i = 0; i + 2 < actions.length; i += 3) {
                const action = { action: actions[i + 1], title: actions[i] };
                if (actions[i + 2]) action.icon = actions[i + 2];
                if (actions[i + 1] === replyActionTag) action.type = 'text';
                options.actions.push(action);
            }
        }
        await registration.showNotification(title ?? '', options);
        if (expirationMs > 0) setTimeout(() => close(id), expirationMs);
        return;
    }

    const options = {};
    if (body) options.body = body;
    if (icon) options.icon = icon;
    if (tag) options.tag = tag;
    const notification = new Notification(title ?? '', options);

    // Reported to .NET once: a click activates the notification, anything else that closes it only closes it.
    let timer = null;
    const done = type => {
        // Another notification may have taken the id since: .NET no longer knows this one, and it must not remove the other's entry.
        if (direct.get(id) !== notification) return;
        direct.delete(id);
        if (timer) clearTimeout(timer);
        const json = JSON.stringify({ type, id, tag: tag ?? null, action: null });
        callback?.invokeMethodAsync(type === 'HandleNotificationClick' ? 'OnClick' : 'OnClose', json).catch(() => { });
    };
    notification.onclick = () => { window.focus(); done('HandleNotificationClick'); notification.close(); };
    notification.onclose = () => done('HandleNotificationClose');
    if (expirationMs > 0) timer = setTimeout(() => notification.close(), expirationMs);
    direct.set(id, notification);
}

export async function close(id) {
    direct.get(id)?.close();
    if (registration) {
        for (const notification of await registration.getNotifications())
            if (notification.data && notification.data.owner === owner && notification.data.id === id) notification.close();
    }
}

export async function closeAll() {
    for (const notification of [...direct.values()]) notification.close();
    if (registration) {
        // Only its own: another tab's notifications are not this page's to close.
        for (const notification of await registration.getNotifications())
            if (notification.data && notification.data.owner === owner) notification.close();
    }
}


// WebAssembly hosts that are not Blazor (Avalonia, Uno): the callbacks are .NET functions passed through JSImport, not a DotNetObjectReference.
export function useCallbacks(onClick, onClose, onReply) {
    return registrations({
        invokeMethodAsync: (name, json, reply) => Promise.resolve(
            name === 'OnClick' ? onClick(json) : name === 'OnClose' ? onClose(json) : onReply(json, reply))
    });
}
