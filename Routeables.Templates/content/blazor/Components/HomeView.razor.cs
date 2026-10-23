using Routeables.Abstraction;
using RouteablesApp.ViewModels;

namespace RouteablesApp.Components;

// The generator cannot read a .razor file, so the pairing with the view model is declared on this C# half of the view.
[RouteableView(typeof(HomeViewModel))]
public partial class HomeView;
