// Asks the browser to confirm before the tab is closed, reloaded or left for another site while the page has unsaved changes.
// The text of the question is the browser's own. Moves inside the application go through the router (CanLeaveAsync), not through here.
// The listener is installed when a warning is asked for, not when the module loads: the browser keeps the module of a page, and a circuit that
// is paused (Blazor.pauseCircuit) and resumed in the same page imports it again without running it again, after dispose() has removed the listener.
let warn = false;
let listening = false;

function onBeforeUnload(event) {
    if (!warn) return;
    event.preventDefault();
    event.returnValue = "";
}

export function setWarn(value) {
    warn = value === true;
    if (warn && !listening) {
        window.addEventListener("beforeunload", onBeforeUnload);
        listening = true;
    }
}

export function dispose() {
    warn = false;
    if (listening) {
        window.removeEventListener("beforeunload", onBeforeUnload);
        listening = false;
    }
}
