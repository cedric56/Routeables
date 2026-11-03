export function isServiceWorkerSupported() {
    if ('serviceWorker' in navigator) {
        return true;
    }
    console.warn("Service workers are not supported in this browser.");
    return false;
}
export async function registerServiceWorker() {
    await navigator.serviceWorker.register("notifications-service-worker.js", { type: 'module' });
}
export function isSupported() {
    return 'Notification' in window;
}
export async function closeNotification(id) {
    const registration = await navigator.serviceWorker.ready;
    const notifications = await registration.getNotifications();
    const idNum = Number(id);
    notifications.forEach(n => {
        if (n.data && n.data.id === idNum) {
            n.close();
        }
    });
}
export async function closeAllNotifications() {
    const registration = await navigator.serviceWorker.ready;
    const notifications = await registration.getNotifications();
    notifications.forEach(n => n.close());
}
export async function showNotification(title, json) {
    if (!isSupported()) {
        console.warn("This browser does not support notifications.");
        return;
    }
    let permission = Notification.permission;
    if (permission === "default") {
        permission = await Notification.requestPermission();
    }
    if (permission !== "granted") {
        console.warn("Notification permission not granted.");
        return;
    }
    const options = JSON.parse(json);
    const notificationsOptions = {
        actions: options.Actions.map(a => ({
            action: a.Action,
            title: a.Title,
            type: a.Type,
            icon: a.Icon
        })),
        body: options.Body || '',
        data: {
            id: options.Data.Id,
            replyActionTag: options.Data.ReplyActionTag || '',
        },
        tag: options.Tag || '',
        icon: options.Icon || '',
        vibrate: options.Vibrations
    };
    const registration = await navigator.serviceWorker.ready;
    await registration.showNotification(title, notificationsOptions);
}
export async function registrations(onclose, onclick, onreply) {
    navigator.serviceWorker.addEventListener('message', function (event) {
        if (event.data && event.data.type === 'OnClose') {
            onclose(JSON.stringify(event.data));
        }
        if (event.data && event.data.type === 'OnClick') {
            onclick(JSON.stringify(event.data));
        }
        if (event.data && event.data.type === 'OnReply') {
            onreply(JSON.stringify(event.data), event.data.reply);
        }
    });
}
