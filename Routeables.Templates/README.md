# Routeables.Templates

`dotnet new` templates for an app with a menu, a page that routes and a page with a route parameter. The view models are the same in all three.

| Short name | Host |
| --- | --- |
| `routeables-blazor` | Blazor Server |
| `routeables-maui` | .NET MAUI |
| `routeables-avalonia` | Avalonia desktop |

```bash
dotnet new install ./Routeables.Templates
dotnet new routeables-avalonia -n MyApp --RepoPath ../Routeables
```

`--RepoPath` is a clone of this repository; leave it out to reference the NuGet packages (not published yet).

There is no Uno template yet: start from `dotnet new unoapp` and follow [`Routeables.Uno`](../Routeables.Uno/README.md).
