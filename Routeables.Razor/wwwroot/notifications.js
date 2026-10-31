// The notification center of the browser for Routeables.Razor (system notifications of IRouteableNotificationService).
// With a service worker (routeables-notifications-worker.js, served from the root of the site) a notification can have action buttons and a reply
// field, and survives the page; without one the Notification API of the page is used and only the click and the close are reported.
let registration = null;
let callback = null;
let registered = false;
const direct = new Map();

// The messages of the service worker (and of the page itself) to .NET: a click, a close or a reply.
export async function registrations(dotNetRef) {
    callback = dotNetRef;
    if (!('serviceWorker' in navigator)) return;
    navigator.serviceWorker.addEventListener('message', function (event) {
        if (event.data && event.data.type === 'HandleNotificationClose') {
            dotNetRef.invokeMethodAsync('OnClose', JSON.stringify(event.data));
        }
        if (event.data && event.data.type === 'HandleNotificationClick') {
            dotNetRef.invokeMethodAsync('OnClick', JSON.stringify(event.data));
        }
        if (event.data && event.data.type === 'HandleNotificationReply') {
            dotNetRef.invokeMethodAsync('OnReply', JSON.stringify(event.data), event.data.reply);
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

// Registers the service worker once; false when there is none (no support, not served, not a secure page): the page then notifies by itself.
export async function register(url) {
    if (registered) return registration !== null;
    registered = true;
    if (!url || !('serviceWorker' in navigator)) return false;
    try {
        await navigator.serviceWorker.register(url, { scope: '/' });
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
        const options = { data: { id }, tag: tag ?? `routeables-${id}` };
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

    // Told once to .NET: a click activates the notification, anything else that closes it only closes it.
    let timer = null;
    const done = type => {
        if (!direct.delete(id)) return;
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
            if (notification.data && notification.data.id === id) notification.close();
    }
}

export async function closeAll() {
    for (const notification of [...direct.values()]) notification.close();
    if (registration) {
        for (const notification of await registration.getNotifications()) notification.close();
    }
}
