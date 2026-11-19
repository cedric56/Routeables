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
        const clients = await tell(message);
        const open = clients.find(client => 'focus' in client);
        if (open) await open.focus();
        else await self.clients.openWindow(self.registration.scope);
    })());
});

self.addEventListener('notificationclose', event => {
    const notification = event.notification;
    event.waitUntil(tell({ type: 'HandleNotificationClose', id: notification.data ? notification.data.id : null, owner: notification.data ? notification.data.owner : null, tag: notification.tag, action: null }));
});
