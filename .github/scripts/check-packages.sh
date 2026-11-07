
#!/usr/bin/env bash

# Fails when a package depends on a Routeables project that is not published on its own:
# those projects are inside the "Routeables" package
# (see src/Routeables.Core/Routeables.nuspec).

set -euo pipefail

dir="${1:-artifacts}"
bad='Routeables\.(Routing|Core|Regions|Services|Locking|Segments|Generated)'
status=0

for nupkg in "$dir"/*.nupkg; do
  deps=$(unzip -p "$nupkg" '*.nuspec' | grep -o 'dependency id="[^"]*"' || true)

  if echo "$deps" | grep -Eq "dependency id=\"$bad\""; then
    echo "::error::$(basename "$nupkg") depends on a project that is inside the Routeables package:"
    echo "$deps" | grep -E "dependency id=\"$bad\""
    status=1
  fi
done

# Every package carries the symbols of its libraries.
for nupkg in "$dir"/*.nupkg; do
  unzip -l "$nupkg" | grep -q '\.pdb$' || {
    echo "::error::$(basename "$nupkg") has no .pdb"
    status=1
  }
done

# Routeables.Abstraction is a package of its own, and the Routeables package depends on exactly its version.
abstraction=$(ls "$dir"/Routeables.Abstraction.[0-9]*.nupkg | grep -v 'symbols' | head -1)
unzip -l "$abstraction" | grep -q ' lib/net10.0/Routeables.Abstraction.dll$' || {
  echo "::error::$(basename "$abstraction") does not contain lib/net10.0/Routeables.Abstraction.dll"
  status=1
}

# The Routeables package must hold its libraries and the generator.
core=$(ls "$dir"/Routeables.[0-9]*.nupkg | grep -v 'symbols' | head -1)

for entry in \
  lib/net10.0/Routeables.Routing.dll \
  lib/net10.0/Routeables.Core.dll \
  lib/net10.0/Routeables.Regions.dll \
  lib/net10.0/Routeables.Services.dll \
  lib/net10.0/Routeables.Locking.dll \
  lib/net10.0/Routeables.Segments.dll \
  analyzers/dotnet/cs/Routeables.Generated.dll \
  buildTransitive/Routeables.targets \
  buildTransitive/Community/RouteableViewModel.cs \
  buildTransitive/Reactive/RouteableViewModel.cs; do

  unzip -l "$core" | grep -q " $entry\$" || {
    echo "::error::$(basename "$core") does not contain $entry"
    status=1
  }
done

# The assembly is in one package only, and the dependency is on exactly the version that was packed.
if unzip -l "$core" | grep -q ' lib/net10.0/Routeables.Abstraction.dll$'; then
  echo "::error::$(basename "$core") contains Routeables.Abstraction.dll, which is a package of its own"
  status=1
fi

version=$(basename "$abstraction" .nupkg | sed 's/^Routeables\.Abstraction\.//')
unzip -p "$core" '*.nuspec' | grep -q "dependency id=\"Routeables.Abstraction\" version=\"\[$version\]\"" || {
  echo "::error::$(basename "$core") does not depend on Routeables.Abstraction [$version]"
  status=1
}

exit "$status"
