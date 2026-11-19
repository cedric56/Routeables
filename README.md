<p align="center">
  <img src="images/logo.svg" alt="Routeables" width="420">
</p>

<p align="center">
  <a href="https://github.com/cedric56/Routeables/actions/workflows/ci.yml"><img src="https://github.com/cedric56/Routeables/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-MIT-5B35E0" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <a href="https://www.nuget.org/packages/Routeables"><img src="https://img.shields.io/nuget/v/Routeables?label=Routeables" alt="NuGet Routeables"></a>
  <a href="https://www.nuget.org/packages/Routeables.Avalonia"><img src="https://img.shields.io/nuget/v/Routeables.Avalonia?label=Routeables.Avalonia" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Blazor"><img src="https://img.shields.io/nuget/v/Routeables.Blazor?label=Routeables.Blazor" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Wpf"><img src="https://img.shields.io/nuget/v/Routeables.Wpf?label=Routeables.Wpf" alt="NuGet"></a>
</p>

# Routeables

**View-model-first routing for .NET 10: Avalonia, WPF, .NET MAUI, Uno Platform, WinUI 3, Blazor and ASP.NET Core.**

A view model asks an `IRouter` to go somewhere. It never touches a page, a frame or a navigation API of the framework, so the same code runs on every host.

Four ideas make the whole model: a **route**, a **view**, a **region** (a zone of the screen that shows pages) and the **router**. Everything else is optional and comes later. A [glossary](docs/guides/glossary.md) explains the words.

[See the Animals sample in your browser](https://cedric56.github.io/Routeables/demo/avalonia/) (Avalonia, WebAssembly).

## Install

```bash
dotnet add package Routeables.Avalonia
```

You need the .NET 10 SDK. The package brings everything else, the source generator included. Another host? Use `Routeables.Wpf`, `.Maui`, `.Uno`, `.WinUI3`, `.Blazor` or `.AspNetCore`: [all the hosts](docs/packages.md).

## Your first page

**1. A page** is a view model with its route, and the view with the matching name:

```csharp
[Routeable("/hello/{name}")]
public sealed class HelloViewModel(string name)
{
    public string Greeting { get; } = $"Hello, {name}!";
}

public partial class HelloView : RouteableFor<HelloViewModel> { public HelloView() => InitializeComponent(); }
```

The router finds `HelloView` by its name and gives it the view model (`ViewModel`, typed).

**2. Start Routeables.** The window it shows is the one you give with `view:`:

```csharp
AppBuilder.Configure<App>()
    .UseRouteables(view: lifetime => new MainWindow());
```

> Remove the creation of the window from `OnFrameworkInitializationCompleted` in your `App`. If both create one, the application opens two.

**3. A zone** in the main view shows the pages, and `Route` is the first one:

```xml
xmlns:routeables="clr-namespace:Routeables.Avalonia;assembly=Routeables.Avalonia"
<routeables:RouteableRegion Route="/hello/World" />
```

**4. Go somewhere** from any view model that asks for an `IRouter` in its constructor:

```csharp
await router.GoToAsync("/hello/Claire");
```

To start from a template instead: `dotnet new install Routeables.Templates`, then `dotnet new routeables-avalonia -n MyApp`.

## Next

- **A task to do?** The [recipes](docs/guides/recipes.md): sign-in guard, master and detail, passing an object, asking before leaving, tabs with their own history.
- **The whole first page, step by step**, with the one-line-per-page registration and the other hosts: [Getting started](docs/getting-started.md).
- **A word you do not know?** The [glossary](docs/guides/glossary.md).
- **Coming from another library?** [Migrating from Prism](docs/guides/migrating-from-prism.md), and [Routeables compared with other routers](docs/guides/comparison.md).
- **Everything it can do:** [What Routeables offers](docs/features.md), one line and one guide for each area.
- **Hosts, packages, samples:** [Hosts and packages](docs/packages.md) and the [samples](samples).
- [Documentation site](https://cedric56.github.io/Routeables/), [contributing](CONTRIBUTING.md), [third-party notices](THIRD-PARTY-NOTICES.md).

Routeables is the navigation layer, dialogs and notifications included. It ships no commands, view-model locator or event aggregator: use CommunityToolkit.Mvvm, ReactiveUI or plain C#.

Released under the [MIT License](LICENSE.txt).
