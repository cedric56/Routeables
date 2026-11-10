using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;
using Routeables.Regions;
using Routeables.Services;

namespace Routeables.Blazor;

/// <summary>
/// What every layout needs, without any markup: the route change state (title, drawer, routing click), the theme,
/// the refresh on view/lock changes, the first-render initialization and the address synchronization.
/// A layout inherits it and defines its own structure (<c>MainLayout</c>, or a MudBlazor layout).
/// </summary>
public abstract class RouteableMainLayoutBase : LayoutComponentBase, IDisposable
{
    /// <summary>The service that applies the theme a view model asked for.</summary>
    [Inject] protected IRouteableRequestedThemeService ThemeService { get; set; } = default!;
    /// <summary>The menu view model: the menu entries and the selected one.</summary>
    [Inject] protected IRouteableMenuViewModel MainViewModel { get; set; } = default!;
    /// <summary>The container of the circuit.</summary>
    [Inject] protected IServiceProvider Provider { get; set; } = default!;
    /// <summary>The view of the page the router shows.</summary>
    protected object? CurrentView => Provider.GetRequiredService<IRouter>().CurrentView;
    /// <summary>Starts Routeables for the circuit (registered by <c>UseRouteablesServer</c>).</summary>
    [Inject] protected Func<IServiceProvider, Task> Initialize { get; set; } = default!;
    /// <summary>Calls JavaScript, to apply the theme.</summary>
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    /// <summary>Keeps the address of the browser and the router in step.</summary>
    [Inject] protected RouteableUrlSync UrlSync { get; set; } = default!;

    /// <summary>
    /// Gets or sets the title displayed in the application's app bar.
    /// </summary>
    public string Title { get; set; } = "Application";

    /// <summary>Whether the drawer is open.</summary>
    protected bool _drawerOpen = true;

    /// <summary>
    /// Toggles the open/closed state of the route change drawer.
    /// </summary>
    protected void DrawerToggle() => _drawerOpen = !_drawerOpen;

    /// <summary>
    /// Switches to the light theme when the dark one is shown, and to the dark one otherwise (the button of the app bar).
    /// </summary>
    protected void ToggleTheme() => ThemeService.Request(Service.IsDarkMode ? RouteableRequestedTheme.Light : RouteableRequestedTheme.Dark);

    /// <summary>
    /// Handles routing link clicks, updating the active routeable item in the manager.
    /// </summary>
    /// <param name="item">The routeable view item selected.</param>
    public void OnNavClick(IRouteableMenuEntry item) => MainViewModel.SelectedEntry = item;

    /// <summary>
    /// Gets the concrete theme service implementation instance.
    /// </summary>
    protected RouteableRequestThemeService Service => (RouteableRequestThemeService)ThemeService;

    /// <summary><c>dark</c> or <c>light</c>: the theme that is shown.</summary>
    protected string ThemeName => Service.IsDarkMode ? "dark" : "light";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        MainViewModel.LockChanged += LockChanged;
        MainViewModel.CurrentViewChanged += CurrentViewChanged;
        MainViewModel.Entries.CollectionChanged += RouteablesChanged;
        Service.RequestedThemeChanged += ThemeChanged;
    }

    // An entry added while the application runs (a module loaded on demand, IRouteableModules.Load): the menu is drawn again.
    private async void RouteablesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => await RefreshAsync("MainLayout.RouteablesChanged");

    private async void LockChanged(object? sender, EventArgs e) => await RefreshAsync("MainLayout.LockChanged");

    private async void CurrentViewChanged(object? sender, object? view)
    {
        // The title first: the render the refresh asks for must show the title of this view, not the one of the previous view.
        Title = MainViewModel.CurrentTitle ?? "Application";
        await RefreshAsync("MainLayout.CurrentViewChanged");
    }

    // These are event handlers (async void): an exception would reach the circuit. A layout removed between the event
    // and the render is not an error.
    private async Task RefreshAsync(string source)
    {
        try { await InvokeAsync(StateHasChanged); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { RouteableErrors.Report(Provider, source, ex); }
    }

    // The history handed over by the prerender (or kept by a paused circuit), when the application asked for it.
    private RouteableHistoryPersistence? _persistence;
    private bool _restorable;

    private RouteableHistoryPersistence? Persistence()
    {
        if (_persistence is not null)
            return _persistence;
        var on = Provider.GetService<RouteableHistoryOptions>() is not null
                 || Provider.GetService<RouteablePrerenderOptions>() is { PersistHistory: true };
        if (!on || Provider.GetService<PersistentComponentState>() is not { } state)
            return null;
        return _persistence = new RouteableHistoryPersistence(state, Provider.GetRequiredService<IRouter>());
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        var persistence = Persistence();
        persistence?.Register();
        if (!IsPrerendering)
        {
            // The circuit that takes over: what the prerender (or the paused circuit) saved is taken now, and brought back after the first render.
            _restorable = persistence?.Take() == true;
            return;
        }

        // Prerendering: OnAfterRenderAsync never runs, so the page is chosen here and the HTML sent to the browser holds it.
        // The circuit that takes over starts again from its own first render.
        var addressChosen = await UrlSync.PrerenderAsync();
        await Initialize(Provider);
        if (addressChosen)
            return;

        var options = Provider.GetService<RouteablePrerenderOptions>();
        var navigation = Provider.GetService<NavigationManager>();
        if (options?.UnknownAddressIsNotFound == true && navigation is not null && !IsRoot(navigation))
        {
            try { navigation.NotFound(); }      // the status of the response becomes 404 (static server rendering)
            catch (InvalidOperationException) { }
            return;
        }

        if (MainViewModel.Entries.FirstOrDefault() is { } first && MainViewModel.SelectedEntry is null)
        {
            var result = await Provider.GetRequiredService<IRouter>().GoToRouteAsync(first);
            if (result.Success)
                MainViewModel.OnRouterMoved(Provider.GetRequiredService<IRouter>().CurrentViewModel);
        }
    }

    // The root address is the default page, never "not found".
    private static bool IsRoot(NavigationManager navigation)
        => navigation.ToBaseRelativePath(navigation.Uri).Split('?', '#')[0].Trim('/').Length == 0;

    // True while the server renders the page for the first response (before any circuit), and only when the application asked for it:
    // registering RouteablePrerenderOptions switches it on. Every static renderer calls itself "Static" (a test renderer too), so the
    // renderer alone does not tell that a page is prerendered for a browser.
    private bool IsPrerendering
    {
        get
        {
            if (Provider.GetService<RouteablePrerenderOptions>() is null)
                return false;
            try { return RendererInfo.Name == "Static"; }
            catch (InvalidOperationException) { return false; }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await FirstRenderAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit closed (page reloaded or closed) while the first render was still going: there is nobody left to start for.
            }
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task FirstRenderAsync()
    {
        // The saved history first: the address that follows is then the one of the page it shows, not a new push.
        var restored = _restorable && _persistence is not null && await _persistence.RestoreAsync();
        if (restored && Provider.GetService<IRouteableMenuViewModel>() is { } main)
            main.OnRouterMoved(Provider.GetRequiredService<IRouter>().CurrentViewModel);
        var addressChosen = await UrlSync.StartAsync();
        addressChosen |= restored;
        await Initialize(Provider);
        await ApplyThemeAsync();
        // The default page only when the address did not already choose one (a link to /elephants/Mammoth).
        // Through SelectedEntry: it is what selects the entry in the menu, and it shows the page.
        // An application without a menu opens its home route here and not from the region: the address decides, and it is applied above.
        if (!addressChosen && !MainViewModel.SelectFirst())
            RegionStart.StartDefault(Provider);
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
        MainViewModel.LockChanged -= LockChanged;
        MainViewModel.CurrentViewChanged -= CurrentViewChanged;
        MainViewModel.Entries.CollectionChanged -= RouteablesChanged;
        Service.RequestedThemeChanged -= ThemeChanged;
        _persistence?.Dispose();
    }
}