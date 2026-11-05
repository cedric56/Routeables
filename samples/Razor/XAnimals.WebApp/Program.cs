using Routeables.AspNetCore;
using Routeables.Generated;
using XAnimals.Common;
using XAnimals.Razor;
using XAnimals.ViewModels;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddRazorRuntimeCompilation();
builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IServiceProvider>(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    return httpContextAccessor.HttpContext?.RequestServices ?? sp;
});
builder.Services.AddRouteablesAspNetCore<MainViewModel>();
builder.Services.AddGeneratedRoutes();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
//app.MapControllerRoute("default", "{**route}", new { controller = "Routeables", action = "Index" });
app.MapGet("/", context =>
{
    context.Response.Redirect("/Index"); // ou la route que tu veux
    return Task.CompletedTask;
});
app.Run();
