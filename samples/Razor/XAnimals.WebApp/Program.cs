using Routeables.AspNetCore;
using Routeables.Generated;
using XAnimals.Common;
using XAnimals.Razor;
using XAnimals.ViewModels;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddRazorRuntimeCompilation();
builder.Services.AddControllersWithViews();
builder.Services.AddRouteablesAspNetCore<MainViewModel>();
//builder.Services.RegisterRoute<XAnimals.Razor.Views.Cats.Views_Cats_Index, CatsViewModel>(route: "/cats");
builder.Services.AddGeneratedRoutes();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute("default", "{**route}", new { controller = "Routeables", action = "Index" });
//app.MapPost("/animals/select", async (HttpContext ctx, AnimalsViewModel service) =>
//{
//    if (!ctx.Request.HasFormContentType)
//        return Results.BadRequest();

//    var form = await ctx.Request.ReadFormAsync();

//    // validate the antiforgery token emitted by Html.AntiForgeryToken()
//    //await ctx.Request.HttpContext.ValidateAntiforgeryTokenAsync(); // hmm, wrong place

//    // validate the antiforgery token!
//    string species = form["species"]!;
//    string name = form["name"]!;
//    var animal = XAnimals.Data.CatData.Cats.FirstOrDefault(c => c.Name == name);
//    await service.OnSelectedAnimalAsync(animal); // your logic here
//    return Results.Redirect("/"); // or re-render
//});
app.Run();
