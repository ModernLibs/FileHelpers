#!/usr/bin/env bash
# build-baselines.sh <out-dir> [<package-version> [<control-tag>]]
#
# Builds the two 4.9.1 baselines of a Step 0 round (T12, design 2.5 item 9 and 3.5) next to the folders of
# build-variants.sh. The benchmark project is this repository's in every folder; only the library differs:
#
#   <out-dir>/pkg-net10             the published package (default 4.9.1), benchmark exe on .NET 10
#   <out-dir>/pkg-net11             the same package (its lib/net10.0), benchmark exe on .NET 11
#   <out-dir>/control-net10         the <control-tag> (default v4.9.1) source, built with this repository's SDK
#   <out-dir>/control-net11-ra-on   the same source compiled for net11.0 with runtime async on
#   <out-dir>/control-net11-ra-off  the same source compiled for net11.0 with runtime async off
#
# The package build uses -p:FileHelpersPackageVersion, the control builds -p:FileHelpersSourceRoot (see the
# benchmark project file). The control source is a clone of this repository at <control-tag> with
# control-v4.9.1.patch applied: global.json names this SDK, and the library targets net10.0;net11.0 with the
# runtime-async switch of 4.10. The tag's source has neither, and it cannot be compiled for net11.0 without them.
# No other file of the tag is changed; the patch is applied with git, so a tag whose files differ stops the build.
#
# After each build, verify-variant reads the library DLL and the script stops unless it is the expected variant.
# The package folders must hold the package's own DLL (SHA-256 compared with the NuGet cache). Each folder gets
# a variant.txt that ab.py reads: the checked line, the commit of the benchmark code (marked "(uncommitted
# changes)" when this tree differs from it), the SDK, the library and its SHA-256.
set -euo pipefail

if [[ $# -lt 1 || $# -gt 3 ]]; then
    echo "usage: $0 <out-dir> [<package-version> [<control-tag>]]" >&2
    exit 2
fi

package_version="${2:-4.9.1}"
control_tag="${3:-v4.9.1}"
step="setup"
trap 'echo "error: build-baselines.sh failed during: $step (exit $?)" >&2' ERR

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
mkdir -p "$1"
out="$(cd "$1" && pwd)"
project="$repo/benchmarks/ModernLibs.FileHelpers.Benchmarks/ModernLibs.FileHelpers.Benchmarks.csproj"
patch_file="$repo/benchmarks/tools/control-v4.9.1.patch"
tool_dir="$out/.tools/verify-variant-baselines"
control_src="$out/.control-src"

# name | target framework | extra build arguments | expected verify-variant output
variants=(
    "pkg-net10|net10.0|-p:FileHelpersPackageVersion=$package_version|tfm=net10.0 runtime-async=off"
    "pkg-net11|net11.0|-p:FileHelpersPackageVersion=$package_version|tfm=net10.0 runtime-async=off"
    "control-net10|net10.0|-p:FileHelpersSourceRoot=$control_src|tfm=net10.0 runtime-async=off"
    "control-net11-ra-on|net11.0|-p:FileHelpersSourceRoot=$control_src|tfm=net11.0 runtime-async=on"
    "control-net11-ra-off|net11.0|-p:FileHelpersSourceRoot=$control_src -p:FileHelpersRuntimeAsync=false|tfm=net11.0 runtime-async=off"
)

# Delete every folder of an earlier run first, so a failed run leaves nothing that still looks valid.
for entry in "${variants[@]}"; do
    IFS='|' read -r name _ <<< "$entry"
    rm -rf "${out:?}/$name" "$out/.artifacts/$name"
done
rm -rf "$tool_dir" "$out/.artifacts/verify-variant-baselines" "$control_src"

step="build verify-variant"
dotnet build "$repo/benchmarks/tools/verify-variant/verify-variant.csproj" -c Release \
    -o "$tool_dir" --artifacts-path "$out/.artifacts/verify-variant-baselines" -nologo -v:q

step="read commit and SDK"
commit="$(git -C "$repo" rev-parse HEAD)"
if [[ -n "$(git -C "$repo" status --porcelain)" ]]; then
    commit="$commit (uncommitted changes)"
fi
sdk="$(cd "$repo" && dotnet --version)"

step="clone $control_tag"
tag_commit="$(git -C "$repo" rev-parse --verify "$control_tag^{commit}")"
git clone --quiet --no-checkout "$repo" "$control_src"
git -C "$control_src" checkout --quiet --detach "$tag_commit"
step="apply $(basename "$patch_file") to $control_tag"
git -C "$control_src" apply --whitespace=nowarn "$patch_file"
control_changes="$(git -C "$control_src" diff --stat | tail -n 1 | sed 's/^ *//')"

step="locate the NuGet cache"
packages="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages: *//p')"

for entry in "${variants[@]}"; do
    IFS='|' read -r name tfm extra expected <<< "$entry"
    step="build $name"
    # $extra holds one or two arguments without spaces (the paths come from pwd and must not contain spaces).
    # shellcheck disable=SC2086
    dotnet build "$project" -c Release -f "$tfm" $extra \
        -o "$out/$name" --artifacts-path "$out/.artifacts/$name" -nologo -v:q
    step="verify $name"
    library="$out/$name/ModernLibs.FileHelpers.dll"
    actual="$(dotnet "$tool_dir/verify-variant.dll" "$library")"
    echo "$name: $actual"
    if [[ "$actual" != "$expected" ]]; then
        echo "error: $name: expected '$expected', got '$actual'" >&2
        exit 1
    fi
    sha="$(sha256sum "$library" | cut -d ' ' -f 1)"
    case "$name" in
        pkg-*)
            package_dll="${packages%/}/modernlibs.filehelpers/$package_version/lib/net10.0/ModernLibs.FileHelpers.dll"
            package_sha="$(sha256sum "$package_dll" | cut -d ' ' -f 1)"
            if [[ "$sha" != "$package_sha" ]]; then
                echo "error: $name: $library is not the DLL of package $package_version ($package_dll)" >&2
                exit 1
            fi
            source_line="ModernLibs.FileHelpers $package_version package (lib/net10.0)"
            ;;
        control-*)
            source_line="$control_tag source ($tag_commit) with $(basename "$patch_file") ($control_changes)"
            ;;
    esac
    printf '%s: %s\ncommit=%s\nsdk=%s\nlibrary=%s\nlibrary_sha256=%s\n' \
        "$name" "$actual" "$commit" "$sdk" "$source_line" "$sha" > "$out/$name/variant.txt"
done
