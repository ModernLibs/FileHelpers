#!/usr/bin/env bash
# Usage: run.sh <package-dir> <version>
# Packs the library as <version> into <package-dir>, restores a net10.0 library (Lib10) that references it
# and a net11.0 app (App11) that references Lib10, then runs the app. Exit 0 = the app got lib/net11.0 and
# read and wrote through descriptors the generator emitted for net10.0.
# Writes only: <package-dir> (nupkgs, nuget config, private packages folder), the library's own bin/obj,
# and this folder's bin/obj. Never touches the global NuGet config or ~/.nuget/packages.
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "usage: $0 <package-dir> <version>" >&2
  exit 64
fi

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
mkdir -p "$1"
pkgdir="$(cd "$1" && pwd)"
version="$2"
packages="$pkgdir/packages"
config="$pkgdir/nuget.config"

# Clean rerun: no stale package of this version, no stale private cache.
rm -rf "$packages"
rm -f "$pkgdir/ModernLibs.FileHelpers.$version.nupkg" "$pkgdir/ModernLibs.FileHelpers.$version.snupkg"

dotnet pack "$repo/src/ModernLibs.FileHelpers" -c Release -p:Version="$version" -o "$pkgdir"

# The package folder is the only extra source (nuget.org stays for everything else).
cat > "$config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="smoke-local" value="$pkgdir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

dotnet restore "$here/App11/App11.csproj" --configfile "$config" --packages "$packages" -p:FhVersion="$version"
dotnet run --project "$here/App11/App11.csproj" -c Release --no-restore -p:FhVersion="$version"
