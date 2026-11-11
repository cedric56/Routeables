# Versioning

Routeables follows [Semantic Versioning 2.0.0](https://semver.org/). All the packages of the repository are released together with the same number, set in `Directory.Build.props` (`VersionPrefix`). Changes are listed in [CHANGELOG.md](CHANGELOG.md).

## What the number promises

| Change | Version part |
|--------|--------------|
| A public type or member is removed or renamed, its signature or its documented behaviour changes, a route that matched stops matching, a saved history or snapshot can no longer be read | **Major** |
| A public type or member, a package, a host, a diagnostic or a template is added; code that compiled keeps compiling and behaving the same | **Minor** |
| A bug is fixed, a document or a message is improved | **Patch** |

The public API is what is public in the `Routeables.*` packages, the code the generators write (`AddGeneratedRoutes`, `GoTo{Name}Async`, the `[Persist]` registration), the `ROUT` diagnostic ids and what they mean, and the format of the saved history and snapshots. What is `internal` is not (the repository's assemblies see each other through `InternalsVisibleTo`; a package must be updated with its siblings, which the dependencies already require).

## Diagnostics

A new `ROUT` rule appears as a warning or an info in a minor version and never as an error, except a rule for code that could not work anyway. A rule is promoted to an error only in a major version. The rules are listed in [Diagnostics](docs/guides/diagnostics.md) and in `AnalyzerReleases.Shipped.md`.

## Deprecation

An API is marked `[Obsolete]` with the version that replaces it, stays for at least one minor version, and is removed in the next major version. The CI build fails on use of an obsolete API inside the repository.

## Previews

A preview is built with `-p:VersionSuffix=preview.1` (`2.1.0-preview.1`): nothing to edit in the repository, and no promise on the API until the stable version.

## Releasing

1. Move the rules of `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md`, under `## Release x.y.z`.
2. Rename `Unreleased` in the changelog to `x.y.z - date`.
3. Set `VersionPrefix` and the `Routeables.*` versions in the `templates/*/content/*.csproj` files.
4. Tag `vx.y.z`, then pack and push the packages.
