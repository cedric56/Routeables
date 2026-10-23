# Routeables.Templates

`dotnet new` templates that start a Routeables application with a menu, a page that routes and a page with a route parameter. The view models have no UI code and are identical in the three templates.

| Template | Short name | Host |
| --- | --- | --- |
| Routeables Blazor Server app | `routeables-blazor` | Blazor Server |
| Routeables .NET MAUI app | `routeables-maui` | .NET MAUI (Android, iOS, Mac Catalyst, Windows) |
| Routeables Avalonia desktop app | `routeables-avalonia` | Avalonia |

```bash
dotnet new install ./Routeables.Templates
dotnet new routeables-avalonia -n MyApp --RepoPath ../Routeables
```

`--RepoPath` is the path to a clone of the repository, and the app references its projects. Leave it out to reference the NuGet packages (not published yet; the version in the templates is a placeholder).

Each template references the host package, its optional `.Services` package (dialogs and notifications; remove the `UseRouteablesServices` line and the reference if you do not need them) and the source generator. The MVVM library is yours to add.

The Blazor and MAUI templates use the views' names to find them (`HomeViewModel` is shown by `HomeView`); the Razor views carry `[RouteableView(...)]` on the C# half of the view (`HomeView.razor.cs`), because the generator cannot read a `.razor` file.
