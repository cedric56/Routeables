using Routeables.AspNetCore;
using Routeables.Generated;
using XAnimals.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Routeables
builder.Services.AddRouteablesAspNetCore<MainViewModel>(services =>
{
    // services.AddRouteCoordinator(); // seulement si tu en as besoin
});

builder.Services.AddGeneratedRoutes();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// Important : mapper les deux
app.MapRazorPages();
app.MapControllers();

// Optionnel mais recommandé : page par défaut
//app.MapGet("/", context =>
//{
//    context.Response.Redirect("/cats"); // ou la route que tu veux
//    return Task.CompletedTask;
//});

app.Run();