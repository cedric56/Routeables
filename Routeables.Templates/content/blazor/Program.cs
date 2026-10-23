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

builder.Services.UseRouteables(Runtime.BlazorServer);       // the router
builder.Services.UseRouteablesServices();                   // optional: dialogs, notifications and the default layout
builder.Services.AddGeneratedRoutes();                      // the routes declared by attributes
builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Scoped);   // one menu per circuit

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
