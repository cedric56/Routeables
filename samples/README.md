# Samples

One application, **XAnimals** (cats, dogs, monkeys, a menu, history, regions, tabs, dialogs, notifications, a guard and a module loaded on demand), written once and shown on every host. The view models, the data and the routes are the same everywhere (`Bases/XAnimals.ViewModels`); each host only has its own views.

Every command is run from the root of the repository. The SDK is the one of `global.json` (.NET 10).

## Where to start

| You want to see | Run |
| --- | --- |
| The shortest way in, on any machine | [Avalonia desktop](#avalonia) |
| A web application | [Blazor Server](#blazor) |
| Everything in a browser, with no server | [Blazor WebAssembly](#blazor) or [Avalonia Browser](#avalonia) |

## The hosts

"In CI" means the build of the sample is checked by the workflow of the repository. It does not mean the sample was looked at on a device: the screens of the hosts were not all reviewed by the same eyes.

| Sample | Folder | Needs | In CI |
| --- | --- | --- | --- |
| [Avalonia desktop](#avalonia) | `XAnimals.Avalonia.Desktop` | .NET 10 | build, UI tests |
| [Avalonia browser](#avalonia) | `XAnimals.Avalonia.Browser` | `wasm-tools` workload | build |
| Avalonia Android / iOS | `XAnimals.Avalonia.Android`, `XAnimals.Avalonia.iOS` | the Android / iOS workload, a device or an emulator | build |
| [Blazor Server](#blazor) | `Blazor/XAnimals.BlazorApp` | .NET 10 | build, browser tests (Playwright) |
| [Blazor WebAssembly](#blazor) | `Blazor/XAnimals.BlazorWebAssembly` | `wasm-tools` workload to publish | build, publish |
| Blazor Hybrid (MAUI) | `Blazor/XAnimals.MauiHybrid` | `maui` workload | build (Android, Mac Catalyst) |
| [Razor Pages](#razor-pages) | `Razor/XAnimals.WebApplication` | .NET 10 | not built |
| [WPF](#wpf) | `XAnimals.Wpf` | Windows | build, device tests |
| [WinUI 3](#winui-3) | `XAnimals.WinUI3` | Windows 10 19041 or later | build |
| [Uno Platform](#uno-platform) | `XAnimals.Uno` | the Uno SDK (`global.json`); a head per target | build, UI tests |
| [.NET MAUI](#net-maui) | `XAnimals.Maui` | `maui` workload | build (Android, Mac Catalyst, iOS) |

The folders `Controls` and `Bases/*.Controls` hold the controls of each platform that the views share. `Modules` holds the Birds module (`XAnimals.Birds` is the shared part, one project per host serves its views): it is loaded when the user asks for it.

## How to run each one

### Avalonia

```
dotnet run --project samples/XAnimals.Avalonia.Desktop
```

The browser version needs the `wasm-tools` workload (`dotnet workload install wasm-tools`):

```
dotnet run --project samples/XAnimals.Avalonia.Browser
```

Android and iOS: open `XAnimals.Avalonia.Android` or `XAnimals.Avalonia.iOS` in your IDE and start an emulator or a device (iOS needs a Mac).

### Blazor

Server (the application listens on `http://localhost:5205`):

```
dotnet run --project samples/Blazor/XAnimals.BlazorApp
```

WebAssembly:

```
dotnet run --project samples/Blazor/XAnimals.BlazorWebAssembly
```

`XAnimals.Blazor` is the shared library of both (the components and the layout). Blazor Hybrid (`XAnimals.MauiHybrid`) is started like a MAUI application.

### Razor Pages

The page-per-route host, where a form posts and the router redirects:

```
dotnet run --project samples/Razor/XAnimals.WebApplication
```

It listens on `http://localhost:5197`.

### WPF

Windows only:

```
dotnet run --project samples/XAnimals.Wpf
```

### WinUI 3

Windows only. Build it in your IDE for a platform (x64, x86 or ARM64) and start it from there, or:

```
dotnet build samples/XAnimals.WinUI3 -c Debug
```

### Uno Platform

One project, several heads. Pick the target framework you want:

```
dotnet run --project samples/XAnimals.Uno -f net10.0-desktop
dotnet run --project samples/XAnimals.Uno -f net10.0-browserwasm
```

`net10.0-android` and `net10.0-ios` need their workloads and a device or an emulator. On a machine without the Windows toolchain the repository builds it with `-p:CiBuild=true` (see `.github/workflows/ci.yml`).

### .NET MAUI

```
dotnet workload install maui
dotnet build samples/XAnimals.Maui -f net10.0-android
```

The targets are Android on every machine, iOS and Mac Catalyst off Linux, and Windows on Windows. Start it from your IDE on an emulator or a device.

## What to look at

| In the app | What it shows | Where the code is |
| --- | --- | --- |
| Menu | The entries of the main view model, the theme | `ViewModels/MainViewModel.cs` |
| Cats, Dogs, Monkeys, Pandas... | A list, a detail with parameters in the route, a typed route | `ViewModels/Animals/` |
| History | Push, back, forward, move to an entry; the transitions | `ViewModels/HistoryViewModel.cs` |
| Regions, Tabs | Several routers at once, a router per tab | `ViewModels/RegionsViewModel.cs`, `TabsViewModel.cs` |
| Adopt a pet | State shared between pages (the pets that were adopted) | `ViewModels/AdoptViewModel.cs` |
| Dialogs | Message, content, task, progress and modal dialogs | `ViewModels/DialogsViewModel.cs` |
| Members | A guard: the page asks for a sign-in | `ViewModels/MembersViewModel.cs` |
| Birds | A module loaded on demand | `Modules/` |

## Not here

A sample made of one feature alone (only the regions, only the dialogs) does not exist yet: they all live in XAnimals. The tests under `tests/` are the smallest examples of each behaviour.
