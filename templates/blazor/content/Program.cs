using Routeables.Abstraction.Registrations;
using Routeables.Blazor;
using RouteablesApp.Components;
using RouteablesApp.Pages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Routeables: one router and one history per circuit. Each page is a view and its view model, with the route of the page.
// (A view written in Razor is registered by name here: the source generator cannot read the Razor files of its own project.)
builder.Services.UseRouteablesServer(services =>
{
    services.RegisterRoute<HomeView, HomeViewModel>(route: "/");
    services.RegisterRoute<HelloView, HelloViewModel>(route: "/hello/{name}");
}); 

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(RouteableMainLayoutBase).Assembly);

app.Run();
