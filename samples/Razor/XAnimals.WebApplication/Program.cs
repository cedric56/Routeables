using Routeables.AspNetCore;
using XAnimals.Razor;
using XAnimals.Razor.Session;
using Routeables.Generated;
using XAnimals.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRouteablesAspNetCore<MainViewModelWithTabs>();
// The view models of the sample, the dialogs and notifications of a web page, and one session in memory per visitor.
builder.Services.AddAnimalsWeb();
builder.Services.AddGeneratedRoutes();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
// The routers and the view models of a visitor stay in memory between two requests: from here, @inject IRouter is the router of the visitor.
app.UseVisitorSessions();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();