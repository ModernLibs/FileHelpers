#!/usr/bin/env bash
# round.sh <round-name> <commit> <filter-file> [--dry] [--launches N] [--job NAME] [--vm NAME] [--root DIR]
#
# One benchmark round on one machine (T12, design 3.5 step 3): builds every configuration of <commit>, checks
# them, and runs ab.py for each A/B pair and filter line. Run it once per VM, on an otherwise idle machine.
#
#   <round-name>   name of the round, used in the result path (for example step0-2026-11-12)
#   <commit>       commit of this repository to measure (a SHA, tag or branch; resolved here, recorded in full)
#   <filter-file>  benchmark globs and the pairs that run them (benchmarks/tools/filters-step0.txt describes the
#                  format); a relative path is looked up in the current directory, then in the clone
#   --dry          local smoke run, never evidence (D1): ab.py --smoke, 5 launches, BenchmarkDotNet job Dry, and
#                  the AOT harness with 10,000 rows, 1 warm-up read and 3 timed reads
#   --launches N   launches per side (default 7, the B4 minimum of ab.py; --dry: 5)
#   --job NAME     BenchmarkDotNet job (passed after --; default: BenchmarkDotNet's default job; --dry: Dry)
#   --vm NAME      machine name in the result path (default: the host name)
#   --root DIR     working root (default: $HOME/fh-round); results go to DIR/raw/<round-name>/<vm>/
#
# Steps:
#   1. machine.txt: lscpu, uname -a, OS, memory, dotnet --info, the arguments of this run
#   2. a clean clone of this repository at <commit> (the v4.9.1 tag must exist in it)
#   3. build-variants.sh (net10, net11-ra-on, net11-ra-off), build-baselines.sh (pkg-net10, pkg-net11,
#      control-net10, control-net11-ra-on, control-net11-ra-off) and build-aot-variants.sh (the Native AOT harness
#      of T47 in aot/net10, aot/net11-ra-on, aot/net11-ra-off, aot/pkg-net10, aot/pkg-net11; needs clang or gcc),
#      one build at a time; then the clone must still be clean
#   4. checks before any measurement, for every folder: verify-variant on its library DLL and the start-up lines
#      of a process started from it must match the folder's name (else "VARIANT MISMATCH <dir>"); the library
#      DLLs of all folders differ, except the two package folders, which hold the same package DLL;
#      --check-competitors and --check-prototypes must exit 0 (else "CHECK FAILED <check> <dir>"); every AOT
#      folder's variant.txt and the start-up lines of its executable (--info) must match its name and say
#      native-aot=yes (else "VARIANT MISMATCH <dir>")
#   5. ab.py for every pair and filter line; a glob that selects nothing in one build of a pair is recorded in
#      not-run.txt and skipped. Filter lines of group "aot" run in the aot-* pairs with ab.py --aot (the glob
#      selects among the harness's scenarios); all other groups run in the BenchmarkDotNet pairs
#   6. the last line is "ROUND COMPLETE <n> result files"
#
# Everything goes under raw/<round-name>/<vm>/: machine.txt, variants.txt, filters.txt, commands.txt, timing.txt,
# not-run.txt, build.log, checks/, and <pair>/<line>-<group>-<glob>.json with ab.py's <json>.runs/ folders.
# benchmarks/tools/round-report.py raw/<round-name> builds the report tables from them.
set -euo pipefail

usage() {
    echo "usage: $0 <round-name> <commit> <filter-file> [--dry] [--launches N] [--job NAME] [--vm NAME] [--root DIR]" >&2
    exit 2
}

[[ $# -ge 3 ]] || usage
round="$1" commit_arg="$2" filter_arg="$3"
shift 3
dry=0 launches="" job="" vm="$(hostname -s)" root="$HOME/fh-round"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry) dry=1; shift ;;
        --launches) [[ $# -ge 2 ]] || usage; launches="$2"; shift 2 ;;
        --job) [[ $# -ge 2 ]] || usage; job="$2"; shift 2 ;;
        --vm) [[ $# -ge 2 ]] || usage; vm="$2"; shift 2 ;;
        --root) [[ $# -ge 2 ]] || usage; root="$2"; shift 2 ;;
        *) usage ;;
    esac
done
if [[ $dry -eq 1 ]]; then
    launches="${launches:-5}"
    job="${job:-Dry}"
else
    launches="${launches:-7}"
fi
[[ "$round" =~ ^[A-Za-z0-9._-]+$ && "$vm" =~ ^[A-Za-z0-9._-]+$ ]] || {
    echo "error: round and VM names may only contain letters, digits, '.', '_' and '-'" >&2; exit 2; }

step="setup"
trap 'echo "ROUND FAILED during: $step (exit $?)" >&2' ERR

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
raw="$root/raw/$round/$vm"
work="$root/work/$round/$vm"
src="$work/src"
bin="$work/bin"

if [[ -e "$raw" && -n "$(ls -A "$raw" 2>/dev/null)" ]]; then
    echo "error: $raw is not empty; a round never overwrites results (use another round name or VM name)" >&2
    exit 1
fi
mkdir -p "$raw" "$raw/checks"

# --- 1. machine record -------------------------------------------------------------------------------------
step="machine record"
sha="$(git -C "$repo" rev-parse --verify "$commit_arg^{commit}")"
{
    echo "round: $round"
    echo "vm: $vm"
    echo "started: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    if [[ $dry -eq 1 ]]; then
        echo "mode: DRY RUN (ab.py --smoke, job $job): a smoke check of the script, not evidence (D1)"
    else
        echo "mode: round (B4: $launches launches per side)"
    fi
    echo "commit: $sha (requested as $commit_arg)"
    echo "repository: $repo"
    echo "launches: $launches"
    echo "job: ${job:-BenchmarkDotNet default}"
    echo
    echo "== uname -a"; uname -a
    echo; echo "== /etc/os-release"; cat /etc/os-release 2>/dev/null || echo "(none)"
    echo; echo "== lscpu"; lscpu
    echo; echo "== nproc"; nproc
    echo; echo "== free -m"; free -m
    echo; echo "== dotnet --info"; dotnet --info
    echo; echo "== python3 --version"; python3 --version
    echo; echo "== git --version"; git --version
    echo; echo "== native toolchain (Native AOT publish)"; clang --version 2>/dev/null || gcc --version 2>/dev/null \
        || echo "(no clang or gcc on PATH: the AOT publish will fail)"
} > "$raw/machine.txt" 2>&1
echo "machine record: $raw/machine.txt"

# --- 2. clean clone at <commit> ----------------------------------------------------------------------------
step="clone $sha"
rm -rf "$work"
mkdir -p "$work"
git clone --quiet --no-checkout "$repo" "$src"
git -C "$src" checkout --quiet --detach "$sha"
git -C "$src" rev-parse --verify --quiet "v4.9.1^{commit}" > /dev/null || {
    echo "error: tag v4.9.1 is not in $repo (the control build needs it)" >&2; exit 1; }

step="filter file"
if [[ -f "$filter_arg" ]]; then
    filter_file="$(cd "$(dirname "$filter_arg")" && pwd)/$(basename "$filter_arg")"
elif [[ -f "$src/$filter_arg" ]]; then
    filter_file="$src/$filter_arg"
else
    echo "error: filter file $filter_arg not found (current directory or clone)" >&2
    exit 1
fi
cp "$filter_file" "$raw/filters.txt"

# --- 3. builds, one at a time ------------------------------------------------------------------------------
step="build-variants.sh"
echo "building the 4.10 configurations (log: $raw/build.log)"
"$src/benchmarks/tools/build-variants.sh" "$bin" >> "$raw/build.log" 2>&1
step="build-baselines.sh"
echo "building the 4.9.1 package and control folders (log: $raw/build.log)"
"$src/benchmarks/tools/build-baselines.sh" "$bin" >> "$raw/build.log" 2>&1
step="build-aot-variants.sh"
echo "publishing the Native AOT harness folders (log: $raw/build.log)"
"$src/benchmarks/tools/build-aot-variants.sh" "$bin/aot" >> "$raw/build.log" 2>&1
# After every build, the AOT publishes included: no build may leave a change in the clone.
if [[ -n "$(git -C "$src" status --porcelain)" ]]; then
    echo "error: the clone has uncommitted changes after the builds" >&2
    exit 1
fi

# --- 4. checks before any measurement ----------------------------------------------------------------------
# folder | verify-variant line | .NET major version the process must run on
folders=(
    "net10|tfm=net10.0 runtime-async=off|10"
    "net11-ra-on|tfm=net11.0 runtime-async=on|11"
    "net11-ra-off|tfm=net11.0 runtime-async=off|11"
    "pkg-net10|tfm=net10.0 runtime-async=off|10"
    "pkg-net11|tfm=net10.0 runtime-async=off|11"
    "control-net10|tfm=net10.0 runtime-async=off|10"
    "control-net11-ra-on|tfm=net11.0 runtime-async=on|11"
    "control-net11-ra-off|tfm=net11.0 runtime-async=off|11"
)
declare -A expect_line
declare -A library_sha
verify="$bin/.tools/verify-variant/verify-variant.dll"
checks_cwd="$work/checks-cwd"
mkdir -p "$checks_cwd"

mismatch() {
    echo "VARIANT MISMATCH $1" >&2
    echo "  $2" >&2
    exit 1
}

: > "$raw/variants.txt"
for entry in "${folders[@]}"; do
    IFS='|' read -r name line major <<< "$entry"
    dir="$bin/$name"
    expect_line[$name]="$line"
    step="check $name"
    [[ -f "$dir/ModernLibs.FileHelpers.Benchmarks.dll" ]] || mismatch "$dir" "no benchmark build in the folder"
    actual="$(dotnet "$verify" "$dir/ModernLibs.FileHelpers.dll")" || mismatch "$dir" "verify-variant failed"
    [[ "$actual" == "$line" ]] || mismatch "$dir" "verify-variant: '$actual', expected '$line'"
    [[ -f "$dir/variant.txt" && "$(head -n 1 "$dir/variant.txt")" == "$name: $line" ]] \
        || mismatch "$dir" "variant.txt does not start with '$name: $line'"
    # Start-up lines of a process started from the folder (it lists nothing and exits 1: no benchmark matches).
    startup="$(cd "$checks_cwd" && dotnet "$dir/ModernLibs.FileHelpers.Benchmarks.dll" --list flat \
        --filter 'T12.Round.NoSuchBenchmark' 2>&1 | grep '^// ' || true)"
    printed="$(sed -n 's#^// ModernLibs.FileHelpers variant: ##p' <<< "$startup")"
    assembly="$(sed -n 's#^// ModernLibs.FileHelpers assembly: ##p' <<< "$startup")"
    runtime="$(sed -n 's#^// Runtime: ##p' <<< "$startup")"
    [[ "$printed" == "$line" ]] || mismatch "$dir" "process printed variant '$printed', expected '$line'"
    [[ "$(cd "$(dirname "$assembly")" 2>/dev/null && pwd -P)" == "$(cd "$dir" && pwd -P)" ]] \
        || mismatch "$dir" "process loaded ModernLibs.FileHelpers from '$assembly'"
    [[ "$runtime" == ".NET $major."* ]] || mismatch "$dir" "process ran on '$runtime', expected .NET $major"
    library_sha[$name]="$(sha256sum "$dir/ModernLibs.FileHelpers.dll" | cut -d ' ' -f 1)"
    {
        echo "== $name"
        cat "$dir/variant.txt"
        echo "verify-variant: $actual"
        echo "$startup"
        echo
    } >> "$raw/variants.txt"
done

# AOT folders (build-aot-variants.sh already checked each publish; this checks the folders that will be measured).
aot_folders=(
    "net10|tfm=net10.0 runtime-async=off|10"
    "net11-ra-on|tfm=net11.0 runtime-async=on|11"
    "net11-ra-off|tfm=net11.0 runtime-async=off|11"
    "pkg-net10|tfm=net10.0 runtime-async=off|10"
    "pkg-net11|tfm=net10.0 runtime-async=off|11"
)
for entry in "${aot_folders[@]}"; do
    IFS='|' read -r name line major <<< "$entry"
    dir="$bin/aot/$name"
    step="check aot/$name"
    exe="$dir/ModernLibs.FileHelpers.Benchmarks.Aot"
    [[ -x "$exe" ]] || mismatch "$dir" "no AOT harness executable in the folder"
    [[ -f "$dir/variant.txt" && "$(head -n 1 "$dir/variant.txt")" == "$name: $line" ]] \
        || mismatch "$dir" "variant.txt does not start with '$name: $line'"
    startup="$(cd "$checks_cwd" && "$exe" --info 2>&1 | grep '^// ' || true)"
    printed="$(sed -n 's#^// ModernLibs.FileHelpers variant: ##p' <<< "$startup")"
    build_line="$(sed -n 's#^// Build: ##p' <<< "$startup")"
    runtime="$(sed -n 's#^// Runtime: ##p' <<< "$startup")"
    [[ "$printed" == "$line" ]] || mismatch "$dir" "AOT process printed variant '$printed', expected '$line'"
    [[ "$build_line" == "native-aot=yes "* ]] || mismatch "$dir" "AOT process printed build '$build_line', not native-aot=yes"
    [[ "$runtime" == ".NET $major."* ]] || mismatch "$dir" "AOT process ran on '$runtime', expected .NET $major"
    {
        echo "== aot/$name"
        cat "$dir/variant.txt"
        echo "$startup"
        echo
    } >> "$raw/variants.txt"
done

step="library hashes"
[[ "${library_sha[pkg-net10]}" == "${library_sha[pkg-net11]}" ]] \
    || mismatch "$bin/pkg-net11" "its ModernLibs.FileHelpers.dll differs from pkg-net10's (both must be the package DLL)"
declare -A seen_sha
for entry in "${folders[@]}"; do
    IFS='|' read -r name _ <<< "$entry"
    [[ "$name" == "pkg-net11" ]] && continue
    other="${seen_sha[${library_sha[$name]}]:-}"
    [[ -z "$other" ]] || mismatch "$bin/$name" "same ModernLibs.FileHelpers.dll as $other: two columns would show one build"
    seen_sha[${library_sha[$name]}]="$name"
done

for entry in "${folders[@]}"; do
    IFS='|' read -r name _ <<< "$entry"
    for check in --check-competitors --check-prototypes; do
        step="$check $name"
        log="$raw/checks/$name${check#-}.txt"
        if ! (cd "$checks_cwd" && dotnet "$bin/$name/ModernLibs.FileHelpers.Benchmarks.dll" "$check") > "$log" 2>&1; then
            echo "CHECK FAILED $check $bin/$name (see $log)" >&2
            tail -n 5 "$log" >&2
            exit 1
        fi
    done
done
echo "checks passed: 8 folders, verify-variant, start-up lines, library hashes, --check-competitors, --check-prototypes; 5 AOT folders, variant.txt and --info"

# --- 5. A/B runs ---------------------------------------------------------------------------------------------
# pair | A folder | B folder | harness (order of the runs). The aot-* pairs run filter lines of group "aot" only,
# the others every other group. There is no AOT control build (T47): the AOT 4.9.1 -> 4.10 ratios include the
# compiler and SDK effect.
pairs=(
    "free-gains|pkg-net10|pkg-net11|bdn"
    "sdk-net10|pkg-net10|control-net10|bdn"
    "sdk-net11|pkg-net11|control-net11-ra-on|bdn"
    "ra-control|control-net11-ra-off|control-net11-ra-on|bdn"
    "v410-net10|pkg-net10|net10|bdn"
    "v410-net11|pkg-net11|net11-ra-on|bdn"
    "ra|net11-ra-off|net11-ra-on|bdn"
    "runtime-410|net10|net11-ra-on|bdn"
    "aot-v410-net10|aot/pkg-net10|aot/net10|aot"
    "aot-v410-net11|aot/pkg-net11|aot/net11-ra-on|aot"
    "aot-v410-net11-ra-off|aot/pkg-net11|aot/net11-ra-off|aot"
    "aot-ra|aot/net11-ra-off|aot/net11-ra-on|aot"
)
known_pairs=" $(for p in "${pairs[@]}"; do printf '%s ' "${p%%|*}"; done)"

# Filter lines: <line number> <group> <pairs> <glob>, validated before the first run.
step="read filter file"
mapfile -t filter_lines < <(awk '!/^[[:space:]]*(#|$)/ { print NR, $0 }' "$raw/filters.txt")
[[ ${#filter_lines[@]} -gt 0 ]] || { echo "error: no filter line in $filter_arg" >&2; exit 1; }
for fl in "${filter_lines[@]}"; do
    read -r lineno group pair_list glob extra <<< "$fl"
    if [[ -z "${glob:-}" || -n "${extra:-}" ]]; then
        echo "error: $filter_arg line $lineno: expected '<group> <pairs> <glob>'" >&2; exit 1
    fi
    if [[ "$pair_list" != "all" ]]; then
        IFS=',' read -ra listed <<< "$pair_list"
        for p in "${listed[@]}"; do
            [[ "$known_pairs" == *" $p "* ]] || { echo "error: $filter_arg line $lineno: unknown pair '$p'" >&2; exit 1; }
            if [[ "$group" == "aot" && "$p" != aot-* ]] || [[ "$group" != "aot" && "$p" == aot-* ]]; then
                echo "error: $filter_arg line $lineno: group '$group' cannot run in pair '$p' (group aot runs in the aot-* pairs only)" >&2
                exit 1
            fi
        done
    fi
done

ab_args=(--launches "$launches")
[[ $dry -eq 1 ]] && ab_args+=(--smoke)
bdn_args=()
[[ -n "$job" ]] && bdn_args=(-- --job "$job")
# Arguments of the AOT harness (T47): 100,000 rows, 5 untimed and 20 timed reads per launch; a dry run is shorter.
aot_args=(--rows 100000 --warmup 5 --iterations 20)
[[ $dry -eq 1 ]] && aot_args=(--rows 10000 --warmup 1 --iterations 3)

lists_any() {
    # True when the build in folder $1 has a benchmark method that matches glob $2. The listing process exits 1
    # (it measured nothing, Program.cs), so its output is captured first: in a pipeline, pipefail would turn that
    # exit code into "no match".
    local listing
    listing="$(cd "$checks_cwd" && dotnet "$bin/$1/ModernLibs.FileHelpers.Benchmarks.dll" --list flat \
        --filter "$2" 2>/dev/null || true)"
    grep -Eq '^[A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z0-9_]+$' <<< "$listing"
}

results=0
: > "$raw/not-run.txt"
: > "$raw/commands.txt"
: > "$raw/timing.txt"
for pentry in "${pairs[@]}"; do
    IFS='|' read -r pair a b harness <<< "$pentry"
    for fl in "${filter_lines[@]}"; do
        read -r lineno group pair_list glob <<< "$fl"
        [[ "$pair_list" == "all" || ",$pair_list," == *",$pair,"* ]] || continue
        # "all" names every pair of the line's own harness.
        [[ ( "$harness" == aot && "$group" == aot ) || ( "$harness" == bdn && "$group" != aot ) ]] || continue
        slug="$(printf '%s' "$glob" | sed 's/[^A-Za-z0-9_.-]/_/g; s/^_*//; s/_*$//')"
        out="$raw/$pair/$(printf '%03d' "$lineno")-${group//,/+}-$slug.json"
        step="$pair: $glob"
        missing=""
        if [[ "$harness" == bdn ]]; then
            lists_any "$a" "$glob" || missing="$a"
            lists_any "$b" "$glob" || missing="${missing:+$missing and }$b"
        fi
        if [[ -n "$missing" ]]; then
            printf '%s\t%s\t%s\tno benchmark matches in %s\n' "$pair" "$group" "$glob" "$missing" >> "$raw/not-run.txt"
            echo "not run: $pair $glob (no benchmark matches in $missing)"
            continue
        fi
        mkdir -p "$raw/$pair"
        if [[ "$harness" == aot ]]; then
            # ab.py --aot knows all five AOT folder names (T47 fix round 1), so no --expect-a/--expect-b.
            cmd=(python3 -I "$src/benchmarks/tools/ab.py" --aot --a "$bin/$a" --b "$bin/$b" "${ab_args[@]}"
                 --filter "$glob" --out "$out" -- "${aot_args[@]}")
        else
            cmd=(python3 -I "$src/benchmarks/tools/ab.py" --a "$bin/$a" --b "$bin/$b" "${ab_args[@]}"
                 --filter "$glob" --out "$out")
            [[ "$a" == net10 || "$a" == net11-ra-on || "$a" == net11-ra-off ]] || cmd+=(--expect-a "${expect_line[$a]}")
            [[ "$b" == net10 || "$b" == net11-ra-on || "$b" == net11-ra-off ]] || cmd+=(--expect-b "${expect_line[$b]}")
            cmd+=("${bdn_args[@]}")
        fi
        printf '%q ' "${cmd[@]}" >> "$raw/commands.txt"
        echo >> "$raw/commands.txt"
        echo "== $pair ($a -> $b): $glob"
        started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
        t0="$(date +%s)"
        "${cmd[@]}" > "${out%.json}.log" 2>&1 || {
            echo "ROUND FAILED: ab.py exited non-zero for $pair $glob (see ${out%.json}.log)" >&2
            tail -n 5 "${out%.json}.log" >&2
            exit 1
        }
        printf '%s\t%s\t%s\t%s\t%ss\n' "$pair" "$glob" "$started" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
            "$(( $(date +%s) - t0 ))" >> "$raw/timing.txt"
        tail -n +1 "${out%.json}.log" | grep -v '^\[[AB]\] ' || true
        results=$((results + 1))
    done
done

echo "finished: $(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$raw/machine.txt"
if [[ $results -eq 0 ]]; then
    echo "ROUND FAILED: no result files" >&2
    exit 1
fi
echo "ROUND COMPLETE $results result files"
