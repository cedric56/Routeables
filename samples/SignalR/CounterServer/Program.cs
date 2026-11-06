using CounterServer;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.SignalR;

var builder = WebApplication.CreateBuilder(args);

// No menu: the main view model only opens the page that is asked for. The route is registered by name, one line per page.
builder.Services.AddRouteablesSignalR<MainViewModelDefault>(services =>
    services.RegisterRoute<CounterView, CounterViewModel>(route: "/counter"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapRouteables();   // the hub, at /routeables

app.Run();
