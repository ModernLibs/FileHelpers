#!/usr/bin/env bash
# build-variants.sh <out-dir>
#
# Builds the benchmark project in the three named configurations of the 4.10 design (D6, section 3.5):
#
#   <out-dir>/net10          -f net10.0
#   <out-dir>/net11-ra-on    -f net11.0                                  (runtime async on, what ships)
#   <out-dir>/net11-ra-off   -f net11.0 -p:FileHelpersRuntimeAsync=false (the escape-hatch build)
#
# Each variant gets its own output folder (-o) and its own intermediate folder (--artifacts-path), so no
# variant can reuse another's compiled library. All variant folders are deleted before the first build, so a
# failed run leaves no folder of an earlier run that still looks valid. After each build, verify-variant reads
# the library DLL the variant will load and prints its target framework and runtime-async state; the script
# stops unless that is the configuration the folder is named after. The checked line, the commit (marked
# "(uncommitted changes)" when the tree differs from it) and the SDK version are written to
# <out-dir>/<variant>/variant.txt, which ab.py checks and copies into its result file.
#
# Run the benchmarks of a variant from its folder with the dll (BenchConfig runs them in-process, so the
# measured code is this verified build and nothing is rebuilt from source):
#   dotnet <out-dir>/net11-ra-on/ModernLibs.FileHelpers.Benchmarks.dll --filter '<glob>'
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "usage: $0 <out-dir>" >&2
    exit 2
fi

step="setup"
trap 'echo "error: build-variants.sh failed during: $step (exit $?)" >&2' ERR

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
mkdir -p "$1"
out="$(cd "$1" && pwd)"
project="$repo/benchmarks/ModernLibs.FileHelpers.Benchmarks/ModernLibs.FileHelpers.Benchmarks.csproj"
tool_dir="$out/.tools/verify-variant"

# name | target framework | extra build arguments | expected verify-variant output
variants=(
    "net10|net10.0||tfm=net10.0 runtime-async=off"
    "net11-ra-on|net11.0||tfm=net11.0 runtime-async=on"
    "net11-ra-off|net11.0|-p:FileHelpersRuntimeAsync=false|tfm=net11.0 runtime-async=off"
)

# Delete every variant folder before the first build (review-T08, Important 3).
for entry in "${variants[@]}"; do
    IFS='|' read -r name _ <<< "$entry"
    rm -rf "${out:?}/$name" "$out/.artifacts/$name"
done
rm -rf "$tool_dir" "$out/.artifacts/verify-variant"

step="build verify-variant"
dotnet build "$repo/benchmarks/tools/verify-variant/verify-variant.csproj" -c Release \
    -o "$tool_dir" --artifacts-path "$out/.artifacts/verify-variant" -nologo -v:q

step="read commit and SDK"
commit="$(git -C "$repo" rev-parse HEAD)"
# Untracked files count too (a new source file changes the build); ignored files (bin/, obj/) do not.
if [[ -n "$(git -C "$repo" status --porcelain)" ]]; then
    commit="$commit (uncommitted changes)"
fi
sdk="$(cd "$repo" && dotnet --version)"

for entry in "${variants[@]}"; do
    IFS='|' read -r name tfm extra expected <<< "$entry"
    step="build $name"
    # $extra is empty or one argument without spaces.
    # shellcheck disable=SC2086
    dotnet build "$project" -c Release -f "$tfm" $extra \
        -o "$out/$name" --artifacts-path "$out/.artifacts/$name" -nologo -v:q
    step="verify $name"
    actual="$(dotnet "$tool_dir/verify-variant.dll" "$out/$name/ModernLibs.FileHelpers.dll")"
    echo "$name: $actual"
    if [[ "$actual" != "$expected" ]]; then
        echo "error: $name: expected '$expected', got '$actual'" >&2
        exit 1
    fi
    printf '%s: %s\ncommit=%s\nsdk=%s\n' "$name" "$actual" "$commit" "$sdk" > "$out/$name/variant.txt"
done
