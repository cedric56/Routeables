
#!/usr/bin/env bash

# The dependencies of the "Routeables" package are written in
# Routeables.Core/Routeables.nuspec, because the package holds the
# assemblies of six projects (Routeables.Abstraction is a package of its own). This fails when they differ from
# the PackageReference of those projects.

set -euo pipefail

cd "$(dirname "$0")/../.."

projects="Routing Core Regions Services Locking Segments"

# The versions are in Directory.Packages.props (central package management); a VersionOverride on a reference wins.
version_of() {
  local id="$1" override="$2"
  if [ -n "$override" ]; then
    echo "$override"
    return
  fi
  grep -o "<PackageVersion Include=\"$id\" Version=\"[^\"]*\"" Directory.Packages.props |
    sed -E 's/.*Version="([^"]*)".*/\1/' | head -n 1
}

declared=$(
  for p in $projects; do
    { grep -ho '<PackageReference Include="[^"]*"[^>]*>' "src/Routeables.$p/Routeables.$p.csproj" || true; } | while read -r reference; do
      id=$(echo "$reference" | sed -E 's/.*Include="([^"]*)".*/\1/')
      override=$(echo "$reference" | sed -nE 's/.*VersionOverride="([^"]*)".*/\1/p')
      echo "$id $(version_of "$id" "$override")"
    done
  done |
    sort -u
)

packed=$(
  grep -o '\<dependency id="[^"]*" version="[^"]*"' \
    "src/Routeables.Core/Routeables.nuspec" |
    sed -E 's/.*id="([^"]*)" version="([^"]*)".*/\1 \2/' |
    grep -v '^Routeables\.Abstraction ' |
    sort -u
)

if [ "$declared" != "$packed" ]; then
  echo "::error::The dependencies of Routeables.nuspec differ from the PackageReference of the projects it packs."
  diff <(echo "$declared") <(echo "$packed") || true
  exit 1
fi

echo "Routeables.nuspec dependencies match the projects."
