// Asks the browser to confirm before the tab is closed, reloaded or left for another site, while the page holds changes that are not saved.
// The text of the question is the browser's own. Moves inside the application go through the router (CanLeaveAsync), not through here.
let warn = false;

function onBeforeUnload(event) {
    if (!warn) return;
    event.preventDefault();
    event.returnValue = "";
}

window.addEventListener("beforeunload", onBeforeUnload);

export function setWarn(value) {
    warn = value === true;
}

export function dispose() {
    warn = false;
    window.removeEventListener("beforeunload", onBeforeUnload);
}
