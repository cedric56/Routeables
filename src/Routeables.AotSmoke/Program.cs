using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;
using Routeables.Routing;
using Routeables.Segments;

var services = new ServiceCollection();
services.AddSingleton<IGreeter, Greeter>();
services.RegisterRouting();
services.AddRouteCoordinator();
services.AddGeneratedRoutes();
services.RegisterSegment<InvoicesViewModel>("Invoices");

using var provider = services.BuildServiceProvider();
var router = provider.GetRequiredService<IRouter>();

var failures = new List<string>();

void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
    if (!ok) failures.Add(name);
}

var home = await router.GoToAsync("/home");
Check("route without parameter", home.Success && router.CurrentViewModel is HomeViewModel { Greeting: "hello" });

var order = await router.GoToAsync("/orders/42");
Check("typed route parameter", order.Success && router.CurrentViewModel is OrdersViewModel { Id: 42 });

var coordinator = provider.GetRequiredService<IRouteCoordinator>();
var segment = await coordinator.CoordinateAsync("Invoices");
Check("segment routing", segment.Success && router.CurrentViewModel is InvoicesViewModel);

var back = await router.GoBackAsync();
Check("history", back.Success);

Console.WriteLine(failures.Count == 0 ? "AOT smoke: all good" : $"AOT smoke: {failures.Count} failure(s)");
return failures.Count;

public interface IGreeter { string Hi(); }
public sealed class Greeter : IGreeter { public string Hi() => "hello"; }

public sealed class HomeViewModel(IGreeter greeter) { public string Greeting { get; } = greeter.Hi(); }
public sealed class OrdersViewModel(int id = 0) { public int Id { get; } = id; }

public sealed class InvoicesViewModel(int id = 0) { public int Id { get; } = id; }

[RouteableView(typeof(InvoicesViewModel))]
public sealed class InvoicesView { public object? Context { get; set; } }

[RouteableView(typeof(HomeViewModel), "/home")]
public sealed class HomeView { public object? Context { get; set; } }

[RouteableView(typeof(OrdersViewModel), "/orders/{id:int?}")]
public sealed class OrdersView { public object? Context { get; set; } }
