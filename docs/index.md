---
_layout: landing
---

# Routeables

**View-model-first routing for .NET, with one routing layer for Blazor, .NET MAUI and Avalonia.**

Your view models depend on one `IRouter` interface instead of framework-specific routing APIs. Routes are declared with attributes and
wired by source generators, and a call to a view model can be generated as a typed method that mirrors its constructor.

> [!WARNING]
> Routeables is in preview (`1.0.0-preview.1`): APIs change between commits.

- [Introduction](introduction.md): what it does and how the projects fit together.
- [Getting started](getting-started.md): install, declare routes, set up a host, route.
- [Guides](guides/routes.md): routes, typed routing, arguments, lifecycle, history, regions, hosts.
- [Benchmarks](benchmarks.md) and [host scenarios](host-scenarios.md): how fast the router is, and what to check on each platform.
- [API reference](api/Routeables.Abstraction.yml): every public type.

Source code and issues: [github.com/cedric56/Routeables](https://github.com/cedric56/Routeables).
