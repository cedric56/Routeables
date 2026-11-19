// Moves in the browser's history. A Back or Forward made by the router (GoBackAsync from a view model) is the same move in the browser,
// so its Forward button and its history match what the user went through.
export function go(delta) {
    history.go(delta);
}
