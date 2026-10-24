#!/usr/bin/env bash
# Fails when a package depends on a Routeables project that is not published on its own:
# those projects are inside the "Routeables" package (see Routeables.Core/Routeables.nuspec).
set -euo pipefail
dir="${1:-artifacts}"
bad='Routeables\.(Abstraction|Routing|Core|Regions|Services|Locking|Segments|Generated)'
status=0
for nupkg in "$dir"/*.nupkg; do
  deps=$(unzip -p "$nupkg" '*.nuspec' | grep -o 'dependency id="[^"]*"' || true)
  if echo "$deps" | grep -Eq "dependency id=\"$bad\""; then
    echo "::error::$(basename "$nupkg") depends on a project that is inside the Routeables package:"
    echo "$deps" | grep -E "dependency id=\"$bad\""
    status=1
  fi
done
# The Routeables package must hold its libraries and the generator.
core=$(ls "$dir"/Routeables.[0-9]*.nupkg | grep -v 'symbols' | head -1)
for entry in lib/net10.0/Routeables.Abstraction.dll lib/net10.0/Routeables.Routing.dll lib/net10.0/Routeables.Core.dll \
             lib/net10.0/Routeables.Regions.dll lib/net10.0/Routeables.Services.dll lib/net10.0/Routeables.Locking.dll \
             lib/net10.0/Routeables.Segments.dll analyzers/dotnet/cs/Routeables.Generated.dll buildTransitive/Routeables.targets; do
  unzip -l "$core" | grep -q " $entry\$" || { echo "::error::$(basename "$core") does not contain $entry"; status=1; }
done
exit $status
