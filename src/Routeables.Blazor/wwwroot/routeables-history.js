// Moves in the history of the browser: a Back or a Forward asked by the router (GoBackAsync from a view model) is the same move in the
// browser, so its Forward button and its history stay true to what the user went through.
export function go(delta) {
    history.go(delta);
}
