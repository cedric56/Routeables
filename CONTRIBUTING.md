# Contributing

Issues and pull requests are welcome. As the project is in preview, please open an issue to discuss larger changes first.

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

## Documentation

The guides and the API reference are published at <https://cedric56.github.io/Routeables/>, built with [DocFX](https://dotnet.github.io/docfx/) from the `docs`
folder by the `Docs` workflow. To build them locally:

```powershell
dotnet tool update -g docfx
docfx docs/docfx.json --serve
```

Then open <http://localhost:8080>. The `docs/_site` and `docs/api/*.yml` folders are generated: do not commit them.
