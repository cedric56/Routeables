using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Generated;
using Routeables.Razor;
using Routeables.Razor.Services;
using RouteablesApp.Components;
using RouteablesApp.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.UseRouteablesServer<MainViewModel>(   // the router and the menu (one per circuit)
    services => services.AddGeneratedRoutes());                  // the routes declared by attributes
builder.Services.UseRouteablesServices();                             // optional: dialogs, notifications and the default layout

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(MainLayoutBase).Assembly,        // Routeables.Razor: the catch-all page
        typeof(MainLayout).Assembly);           // Routeables.Razor.Services: the layout and the not-found page

app.Run();
