#!/usr/bin/env bash
# build-aot-variants.sh <out-dir> [--package-version V] [<variant>...]
#
# Publishes the Native AOT timing harness (benchmarks/ModernLibs.FileHelpers.Benchmarks.Aot, T47) once per
# configuration, each into a clean folder of its own, with the folder names of build-variants.sh and of the T12
# baselines, so ab.py --aot checks them as it checks the BenchmarkDotNet folders:
#
#   <out-dir>/net10          this repository's library, net10.0
#   <out-dir>/net11-ra-on    this repository's library, net11.0, runtime async on (what ships)
#   <out-dir>/net11-ra-off   this repository's library, net11.0, -p:FileHelpersRuntimeAsync=false
#   <out-dir>/pkg-net10      the published package (default 4.9.1), harness on .NET 10
#   <out-dir>/pkg-net11      the same package (its lib/net10.0), harness on .NET 11
#
# Without <variant> arguments all five are published; with them, only those (CI publishes two). The runtime
# identifier comes from the machine (linux-x64 or linux-arm64). Every publish runs alone (-v:q), one after the other.
#
# After each publish the script stops unless:
#   - the publish log has no trim or AOT diagnostic (warning/error ILxxxx; TreatWarningsAsErrors fails them first);
#   - the folder holds the native executable and no ModernLibs.FileHelpers.dll (nothing is loaded at run time);
#   - verify-variant, run on the ModernLibs.FileHelpers.dll that the AOT compiler read (the -r: line of its
#     .ilc.rsp file), prints the configuration of the folder's name;
#   - for pkg-*, that DLL is the package's own (SHA-256 compared with the NuGet cache);
#   - the executable, started with --info, prints the same variant line, "native-aot=yes", and runs on the .NET
#     major version of its target framework.
# Each folder then gets a variant.txt that ab.py reads: the checked line, the commit (marked "(uncommitted
# changes)" when the tree differs from it), the SDK, the library, its SHA-256, the RID and "aot=true".
#
# Run: python3 -I benchmarks/tools/ab.py --aot --a <out-dir>/pkg-net10 --b <out-dir>/net10 \
#          --expect-a 'tfm=net10.0 runtime-async=off' --launches 7 --out aot-net10.json -- --rows 100000
set -euo pipefail

usage() {
    echo "usage: $0 <out-dir> [--package-version V] [net10|net11-ra-on|net11-ra-off|pkg-net10|pkg-net11 ...]" >&2
    exit 2
}

[[ $# -ge 1 ]] || usage
out_arg="$1"
shift
package_version="4.9.1"
selected=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --package-version) [[ $# -ge 2 ]] || usage; package_version="$2"; shift 2 ;;
        -*) usage ;;
        *) selected+=("$1"); shift ;;
    esac
done

step="setup"
trap 'echo "error: build-aot-variants.sh failed during: $step (exit $?)" >&2' ERR

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
mkdir -p "$out_arg"
out="$(cd "$out_arg" && pwd)"
project="$repo/benchmarks/ModernLibs.FileHelpers.Benchmarks.Aot/ModernLibs.FileHelpers.Benchmarks.Aot.csproj"
exe_name="ModernLibs.FileHelpers.Benchmarks.Aot"
tool_dir="$out/.tools/verify-variant"

case "$(uname -m)" in
    x86_64) rid="linux-x64" ;;
    aarch64 | arm64) rid="linux-arm64" ;;
    *) echo "error: no runtime identifier for machine '$(uname -m)'" >&2; exit 1 ;;
esac

# name | target framework | extra publish arguments | expected verify-variant output
all_variants=(
    "net10|net10.0||tfm=net10.0 runtime-async=off"
    "net11-ra-on|net11.0||tfm=net11.0 runtime-async=on"
    "net11-ra-off|net11.0|-p:FileHelpersRuntimeAsync=false|tfm=net11.0 runtime-async=off"
    "pkg-net10|net10.0|-p:FileHelpersPackageVersion=$package_version|tfm=net10.0 runtime-async=off"
    "pkg-net11|net11.0|-p:FileHelpersPackageVersion=$package_version|tfm=net10.0 runtime-async=off"
)
variants=()
if [[ ${#selected[@]} -eq 0 ]]; then
    variants=("${all_variants[@]}")
else
    for want in "${selected[@]}"; do
        found=""
        for entry in "${all_variants[@]}"; do
            [[ "${entry%%|*}" == "$want" ]] && found="$entry"
        done
        [[ -n "$found" ]] || { echo "error: unknown variant '$want'" >&2; usage; }
        variants+=("$found")
    done
fi

# Delete the folders of this run before the first publish, so a failed run leaves no folder that looks valid.
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
packages="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages: *//p')"

for entry in "${variants[@]}"; do
    IFS='|' read -r name tfm extra expected <<< "$entry"
    folder="$out/$name"
    artifacts="$out/.artifacts/$name"
    mkdir -p "$artifacts"
    log="$artifacts/publish.log"

    step="publish $name"
    # $extra is empty or one argument without spaces.
    # shellcheck disable=SC2086
    if ! dotnet publish "$project" -c Release -f "$tfm" -r "$rid" $extra \
        -o "$folder" --artifacts-path "$artifacts" -nologo -v:q > "$log" 2>&1; then
        cat "$log" >&2
        echo "error: $name: dotnet publish failed (log: $log)" >&2
        exit 1
    fi
    step="check $name"
    if grep -Eq "(warning|error) IL[0-9]" "$log"; then
        grep -E "(warning|error) IL[0-9]" "$log" >&2
        echo "error: $name: the publish reported trim or AOT diagnostics (log: $log)" >&2
        exit 1
    fi
    exe="$folder/$exe_name"
    [[ -x "$exe" ]] || { echo "error: $name: no native executable $exe" >&2; exit 1; }
    if [[ -e "$folder/ModernLibs.FileHelpers.dll" ]]; then
        echo "error: $name: $folder holds ModernLibs.FileHelpers.dll; a Native AOT folder must not" >&2
        exit 1
    fi

    # The library the AOT compiler compiled in: the one -r: line of its response file that names it.
    mapfile -t rsps < <(find "$artifacts/obj" -name "$exe_name.ilc.rsp")
    [[ ${#rsps[@]} -eq 1 ]] || { echo "error: $name: expected one $exe_name.ilc.rsp, found ${#rsps[@]}" >&2; exit 1; }
    mapfile -t libraries < <(sed -n 's/^-r:\(.*\/ModernLibs\.FileHelpers\.dll\)$/\1/p' "${rsps[0]}")
    [[ ${#libraries[@]} -eq 1 ]] || {
        echo "error: $name: expected one ModernLibs.FileHelpers.dll in ${rsps[0]}, found ${#libraries[@]}" >&2
        exit 1
    }
    library="${libraries[0]}"
    actual="$(dotnet "$tool_dir/verify-variant.dll" "$library")"
    echo "$name: $actual ($library)"
    if [[ "$actual" != "$expected" ]]; then
        echo "error: $name: the compiled-in library is '$actual', expected '$expected'" >&2
        exit 1
    fi
    sha="$(sha256sum "$library" | cut -d ' ' -f 1)"
    case "$name" in
        pkg-*)
            package_dll="${packages%/}/modernlibs.filehelpers/$package_version/lib/net10.0/ModernLibs.FileHelpers.dll"
            if [[ "$sha" != "$(sha256sum "$package_dll" | cut -d ' ' -f 1)" ]]; then
                echo "error: $name: $library is not the DLL of package $package_version ($package_dll)" >&2
                exit 1
            fi
            source_line="ModernLibs.FileHelpers $package_version package (lib/net10.0)"
            ;;
        *)
            source_line="this repository's src/ModernLibs.FileHelpers"
            ;;
    esac

    info="$("$exe" --info)"
    major="${tfm#net}"
    major="${major%%.*}"
    for want in "// ModernLibs.FileHelpers variant: $expected" "// Build: native-aot=yes app-tfm=$tfm rid=$rid" \
        "// Runtime: .NET $major."; do
        if ! grep -qF -- "$want" <<< "$info"; then
            echo "error: $name: '$exe --info' does not print '$want':" >&2
            echo "$info" >&2
            exit 1
        fi
    done

    printf '%s: %s\ncommit=%s\nsdk=%s\nlibrary=%s\nlibrary_sha256=%s\nrid=%s\naot=true\n' \
        "$name" "$actual" "$commit" "$sdk" "$source_line" "$sha" "$rid" > "$folder/variant.txt"
done
