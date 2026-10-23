using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Routeables.Core;
using Routeables.Generated;
using Routeables.Razor;
using Routeables.Razor.Services;
using Xanimals.Razor;
using XAnimals.Razor.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.UseRouteables(Runtime.BlazorWebAssembly).UseRouteablesServices();
builder.Services.AddGeneratedRoutes();   // here, not in XAnimals.Razor: the Razor views of that library are seen as referenced assemblies
builder.Services.UseSharedLibrary();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
