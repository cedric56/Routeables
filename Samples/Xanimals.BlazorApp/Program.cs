using Routeables.Core;
using Routeables.Generated;
using Routeables.Razor;
using Routeables.Razor.Services;
using Xanimals.BlazorApp.Components;
using Xanimals.Razor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.UseRouteables(Runtime.BlazorServer).UseRouteablesServices();
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
        typeof(Routeables.Razor.MainLayoutBase).Assembly,
        typeof(Routeables.Razor.Services.MainLayout).Assembly,
        typeof(Xanimals.Razor.Extensions).Assembly
    );

app.Run();
