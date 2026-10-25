# Contributing

Issues and pull requests are welcome. Please open an issue to discuss larger changes first.

## Building and testing

The projects target **.NET 10**. MAUI also needs the MAUI workload, and Uno the Uno templates and SDK.

```bash
dotnet build Routeables.slnx
dotnet test --project Routeables.Test/Routeables.Test.csproj
```

`dotnet test` runs xunit v3 through Microsoft.Testing.Platform, enabled by `global.json`. On CI, pass `-p:CiBuild=true` so the MAUI and Uno
libraries build only the targets the runner has the workloads for.

Performance and memory checks run outside the test suite, in `Release`:

```bash
dotnet run -c Release --project Routeables.Benchmarks -- --filter "*"
dotnet run -c Release --project Routeables.Benchmarks -- --leak
```

[docs/benchmarks.md](docs/benchmarks.md) explains the benchmarks, and [docs/host-scenarios.md](docs/host-scenarios.md) lists the scenarios to play by hand on each host.

## Packages

The repository has many projects and few packages. `Routeables.Core` is the **`Routeables`** package: it holds the assemblies of Abstraction, Routing, Core, Regions, Services, Locking and Segments, and the generator in `analyzers/`. Its files and dependencies are listed in `Routeables.Core/Routeables.nuspec`: add a new file or change a `PackageReference` of one of those projects there too. Those projects have `IsPackable` off.

The host projects (`Routeables.Razor`, `.Maui`, `.Avalonia`, `.Uno`, `.Wpf`, the Reshapes) are packed as they are; the dialogs and notifications of a host are in its `Services/` folder. A packed project must not reference Abstraction, Routing, Regions, Services, Locking, Segments or Generated directly: pack would make it depend on a package that does not exist. It references `Routeables.Core` (with `PrivateAssets="none"`, so the analyzer reaches the application) and sees the others through it.

```bash
dotnet pack Routeables.Core -c Release -o artifacts     # the Routeables package
dotnet pack Routeables.Razor -c Release -o artifacts    # a host; same for the others
```

The version of every package is in `Directory.Build.props` (`VersionPrefix`, and `VersionSuffix` for a preview; empty it for a release). The CI checks that the dependencies written in the nuspec match the projects (`.github/scripts/check-nuspec.sh`), packs the packages that need no workload, and fails if one of them depends on a project that is not a package.

## Documentation

The guides and the API reference are published at <https://cedric56.github.io/Routeables/>, built with [DocFX](https://dotnet.github.io/docfx/) from the `docs`
folder by the `Docs` workflow. To build them locally:

```powershell
dotnet tool update -g docfx
docfx docs/docfx.json --serve
```

Then open <http://localhost:8080>. The `docs/_site` and `docs/api/*.yml` folders are generated: do not commit them.
