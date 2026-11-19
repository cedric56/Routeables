// The theme the page starts in: the one the application set on the <html> element (data-wui-theme="dark" or "light"), else the system theme.
// It can differ from the system theme: an application that is always dark says so there.
export function current() {
    const defined = document.documentElement.getAttribute("data-wui-theme");
    if (defined === "dark" || defined === "light") return defined;
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}
