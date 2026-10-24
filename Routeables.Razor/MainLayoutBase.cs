using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Razor;

/// <summary>
/// What every layout needs, without any markup: the route change state (title, drawer, routing click), the theme,
/// the refresh on view/lock changes, the first-render initialization and the address synchronization.
/// A layout inherits it and defines its own structure (<c>MainLayout</c>, or a MudBlazor layout).
/// </summary>
public abstract class MainLayoutBase : LayoutComponentBase, IDisposable
{
    [Inject] protected IRouteableRequestedThemeService ThemeService { get; set; } = default!;
    [Inject] protected IMainRouteables Main { get; set; } = default!;
    [Inject] protected IServiceProvider Provider { get; set; } = default!;
    [Inject] protected Func<IServiceProvider, Task> Initialize { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected RouteableUrlSync UrlSync { get; set; } = default!;

    /// <summary>
    /// Gets or sets the title displayed in the application's app bar.
    /// </summary>
    public string Title { get; set; } = "Application";

    protected bool _drawerOpen = true;

    /// <summary>
    /// Toggles the open/closed state of the route change drawer.
    /// </summary>
    protected void DrawerToggle() => _drawerOpen = !_drawerOpen;

    /// <summary>
    /// Handles routing link clicks, updating the active routeable item in the manager.
    /// </summary>
    /// <param name="item">The routeable view item selected.</param>
    public void OnNavClick(IRouteableEntry item) => Main.Routeable = item;

    /// <summary>
    /// Gets the concrete theme service implementation instance.
    /// </summary>
    protected RouteableRequestThemeService Service => (RouteableRequestThemeService)ThemeService;

    protected string ThemeName => Service.IsDarkMode ? "dark" : "light";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        Main.LockChanged += LockChanged;
        Main.CurrentViewChanged += CurrentViewChanged;
        Main.Routeables.CollectionChanged += RouteablesChanged;
        Service.RequestedThemeChanged += ThemeChanged;
    }

    // An entry added while the application runs (a module loaded on demand, IRouteableModules.Load): the menu is drawn again.
    private async void RouteablesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => await RefreshAsync("MainLayout.RouteablesChanged");

    private async void LockChanged(object? sender, EventArgs e) => await RefreshAsync("MainLayout.LockChanged");

    private async void CurrentViewChanged(object? sender, object? view)
    {
        await RefreshAsync("MainLayout.CurrentViewChanged");
        Title = Main.CurrentTitle ?? "Application";
    }

    // These are event handlers (async void): an exception would reach the circuit. A layout removed between the event
    // and the render is not an error.
    private async Task RefreshAsync(string source)
    {
        try { await InvokeAsync(StateHasChanged); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { RouteableErrors.Report(Provider, source, ex); }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var addressChosen = await UrlSync.StartAsync();
            await Initialize(Provider);
            await ApplyThemeAsync();
            // The default page only when the address did not already choose one (a link to /elephants/Mammoth).
            // Through Routeable: it is what selects the entry in the menu, and it shows the page.
            if (!addressChosen)
                Main.SelectFirst();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private async void ThemeChanged(object? sender, RouteableRequestedTheme theme)
    {
        try { await ApplyThemeAsync(); }
        catch (Exception ex) { RouteableErrors.Report(Provider, "MainLayout.ApplyTheme", ex); }

        await RefreshAsync("MainLayout.ThemeChanged");
    }

    /// <summary>
    /// Applies the current dark/light theme attribute to the root HTML document element via JavaScript.
    /// </summary>
    private async Task ApplyThemeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync(
                "document.documentElement.setAttribute",
                "data-wui-theme",
                Service.IsDarkMode ? "dark" : "light");
        }
        catch (JSDisconnectedException) { }
    }

    /// <summary>
    /// Unsubscribes from the event handlers.
    /// </summary>
    public void Dispose()
    {
        Main.LockChanged -= LockChanged;
        Main.CurrentViewChanged -= CurrentViewChanged;
        Main.Routeables.CollectionChanged -= RouteablesChanged;
        Service.RequestedThemeChanged -= ThemeChanged;
    }
}