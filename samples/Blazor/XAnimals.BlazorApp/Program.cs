using Routeables.Blazor;
using Routeables.Generated;
using XAnimals.Blazor;
using XAnimals.BlazorApp.Components;
using XAnimals.ViewModels;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.UseRouteablesServer<MainViewModel>();
builder.Services.WithAppNotifications();
builder.Services.AddGeneratedRoutes();   // here, not in XAnimals.Razor: the Razor views of that library are seen as referenced assemblies
builder.Services.UseSharedLibrary();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    //app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
//app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Routeables.Blazor.RouteableMainLayoutBase).Assembly,
        typeof(XAnimals.Blazor.Extensions).Assembly
    );

app.Run();
