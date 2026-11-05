using Routeables.AspNetCore;
using XAnimals.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services
    .AddRouteablesAspNetCore<MainViewModel>(services =>
    {
        //services.AddRouteCoordinator();
    });

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();