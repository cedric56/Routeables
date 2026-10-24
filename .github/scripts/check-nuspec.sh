#!/usr/bin/env bash
# The dependencies of the "Routeables" package are written in Routeables.Core/Routeables.nuspec, because the package holds the
# assemblies of seven projects. This fails when they differ from the PackageReference of those projects.
set -euo pipefail
cd "$(dirname "$0")/../.."

projects="Abstraction Routing Core Regions Services Locking Segments"
declared=$(for p in $projects; do
  grep -ho '<PackageReference Include="[^"]*" Version="[^"]*"' "Routeables.$p/Routeables.$p.csproj" || true
done | sed -E 's/.*Include="([^"]*)" Version="([^"]*)".*/\1 \2/' | sort -u)
packed=$(grep -o '<dependency id="[^"]*" version="[^"]*"' Routeables.Core/Routeables.nuspec \
  | sed -E 's/.*id="([^"]*)" version="([^"]*)".*/\1 \2/' | sort -u)

if [ "$declared" != "$packed" ]; then
  echo "::error::The dependencies of Routeables.nuspec differ from the PackageReference of the projects it packs."
  diff <(echo "$declared") <(echo "$packed") || true
  exit 1
fi
echo "Routeables.nuspec dependencies match the projects."
