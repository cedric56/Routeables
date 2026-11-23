// Service worker for the system notifications of Routeables.Blazor. Serve it from the root of the site (a worker only controls pages
// under its own path), for example /routeables-notifications-worker.js.
// It tells the open pages what the user did with a notification: HandleNotificationClick, HandleNotificationClose or HandleNotificationReply.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

async function tell(message) {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of clients) client.postMessage(message);
    return clients;
}

// The windows share this worker and it keeps nothing between two wake-ups, so it cannot know which window made a notification. It asks: the
// message of a click reaches every window, and the one that owns the notification answers with RouteablesClaim; that window is the one to focus.
const claims = new Map();
self.addEventListener('message', event => {
    const data = event.data;
    if (!data || data.type !== 'RouteablesClaim' || !event.source) return;
    for (const resolve of claims.get(data.owner) || []) resolve(event.source);
});

// The window that owns the notification, or null when none answers (it was closed, or its script is older than the claim).
function waitForOwner(owner) {
    if (owner === null || owner === undefined) return Promise.resolve(null);
    return new Promise(resolve => {
        const waiting = claims.get(owner) || [];
        const done = client => {
            clearTimeout(timer);
            const rest = (claims.get(owner) || []).filter(w => w !== done);
            if (rest.length > 0) claims.set(owner, rest); else claims.delete(owner);
            resolve(client);
        };
        const timer = setTimeout(() => done(null), 400);
        waiting.push(done);
        claims.set(owner, waiting);
    });
}

self.addEventListener('notificationclick', event => {
    const notification = event.notification;
    notification.close();
    const reply = event.reply === undefined || event.reply === null ? null : event.reply;
    const message = {
        type: reply === null ? 'HandleNotificationClick' : 'HandleNotificationReply',
        id: notification.data ? notification.data.id : null,
        owner: notification.data ? notification.data.owner : null,
        tag: notification.tag,
        action: event.action || null,
        reply
    };
    event.waitUntil((async () => {
        const owned = waitForOwner(message.owner);
        const clients = await tell(message);
        // Its own window first; another one only when that window is gone (the click was then reported to the pages that remain).
        const open = (await owned) || clients.find(client => 'focus' in client);
        if (open) await open.focus();
        else await self.clients.openWindow(self.registration.scope);
    })());
});

self.addEventListener('notificationclose', event => {
    const notification = event.notification;
    event.waitUntil(tell({ type: 'HandleNotificationClose', id: notification.data ? notification.data.id : null, owner: notification.data ? notification.data.owner : null, tag: notification.tag, action: null }));
});
