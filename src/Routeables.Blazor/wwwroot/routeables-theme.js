// The theme the page starts in: the one the application defined on the <html> element (data-wui-theme="dark" or "light", in its own page),
// else the one of the system. It can differ from the system's: an application that is always dark says so there.
export function current() {
    const defined = document.documentElement.getAttribute("data-wui-theme");
    if (defined === "dark" || defined === "light") return defined;
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}
