#!/usr/bin/env bash
# round.sh <round-name> <commit> <filter-file> [--dry] [--resume] [--launches N] [--job NAME] [--max-iterations N]
#          [--vm NAME] [--root DIR]
#
# One benchmark round on one machine (T12, design 3.5 step 3): builds every configuration of <commit>, checks
# them, and runs ab.py for each A/B pair and filter line. Run it once per VM, on an otherwise idle machine, from a
# checkout of <commit> (the script refuses to run when its own copy differs from the commit's).
#
#   <round-name>       name of the round, used in the result path (for example step0-2026-11-12)
#   <commit>           commit of this repository to measure (a SHA, tag or branch; resolved here, recorded in full)
#   <filter-file>      benchmark globs and the pairs that run them (benchmarks/tools/filters-step0.txt describes the
#                      format); a relative path is looked up in the current directory, then in the commit
#   --dry              local smoke run, never evidence (D1): ab.py --smoke, 5 launches, BenchmarkDotNet job Dry, and
#                      the AOT harness with 10,000 rows, 1 warm-up read and 3 timed reads
#   --resume           continue a round under the same names: keeps work/ (clone and builds, if complete) and raw/,
#                      reruns the checks, and skips every pair and line whose result file exists
#   --launches N       launches per side (default 7, the B4 minimum of ab.py; --dry: 5)
#   --job NAME         BenchmarkDotNet job (default: BenchmarkDotNet's default job; --dry: Dry). "Dry" needs --dry: one
#                      cold iteration per launch is never a B4 result
#   --max-iterations N BenchmarkDotNet --maxIterationCount N for every BenchmarkDotNet launch (default: none, i.e.
#                      BenchmarkDotNet's own limit of 100; N must be above 15, BenchmarkDotNet's MinIterationCount);
#                      recorded in machine.txt and in the report's mode cell
#   --vm NAME          machine name in the result path (default: the host name)
#   --root DIR         working root (default: $HOME/fh-round); results go to DIR/raw/<round-name>/<vm>/
#
# Steps:
#   0. checks of the arguments, before anything is written: the commit and the tag v4.9.1 exist, round.sh and
#      round_runs.py are the commit's own, the filter file is valid
#   1. machine.txt: lscpu, uname -a, OS, memory, dotnet --info, the native toolchain, the arguments of this run
#   2. a clean clone of this repository at <commit>
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
#   5. round_runs.py: ab.py for every pair and filter line, the gate lines (group prototype) first. A glob that
#      selects nothing in one build of a pair goes to not-run.txt; a failed ab.py run goes to failed.txt and the
#      round goes on. Filter lines of group "aot" run in the aot-* pairs with ab.py --aot
#   6. the last line is "ROUND COMPLETE <n> result files, <m> FAILED"; the exit code is 1 when m > 0, and
#      round.sh ... --resume then reruns only what failed
#
# Everything goes under raw/<round-name>/<vm>/: machine.txt, variants.txt, filters.txt, commands.txt, timing.txt,
# not-run.txt, failed.txt, build.log, checks/, and <pair>/<line>-<group>-<glob>.json with ab.py's <json>.runs/
# folders. benchmarks/tools/round-report.py raw/<round-name> builds the report tables from them.
set -euo pipefail

usage() {
    echo "usage: $0 <round-name> <commit> <filter-file> [--dry] [--resume] [--launches N] [--job NAME]" \
         "[--max-iterations N] [--vm NAME] [--root DIR]" >&2
    exit 2
}

[[ $# -ge 3 ]] || usage
round="$1" commit_arg="$2" filter_arg="$3"
shift 3
dry=0 resume=0 launches="" job="" max_iterations="" vm="$(hostname -s)" root="$HOME/fh-round"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry) dry=1; shift ;;
        --resume) resume=1; shift ;;
        --launches) [[ $# -ge 2 ]] || usage; launches="$2"; shift 2 ;;
        --job) [[ $# -ge 2 ]] || usage; job="$2"; shift 2 ;;
        --max-iterations) [[ $# -ge 2 ]] || usage; max_iterations="$2"; shift 2 ;;
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
    if [[ "${job,,}" == "dry" ]]; then
        echo "error: --job Dry needs --dry (one cold iteration per launch is never a B4 result)" >&2
        exit 2
    fi
    # ab.py refuses fewer than 7 launches without --smoke (R20); refuse here, before any build (rereview Minor 4).
    if ! [[ "$launches" =~ ^[0-9]+$ ]] || [[ "$launches" -lt 7 ]]; then
        echo "error: --launches $launches is below 7, the B4 minimum; fewer launches only with --dry" >&2
        exit 2
    fi
fi
[[ "$round" =~ ^[A-Za-z0-9._-]+$ && "$vm" =~ ^[A-Za-z0-9._-]+$ ]] || {
    echo "error: round and VM names may only contain letters, digits, '.', '_' and '-'" >&2; exit 2; }
# BenchmarkDotNet refuses a maxIterationCount that is not above its MinIterationCount (15 by default).
[[ -z "$max_iterations" || ( "$max_iterations" =~ ^[1-9][0-9]*$ && "$max_iterations" -gt 15 ) ]] || {
    echo "error: --max-iterations needs an integer above 15 (BenchmarkDotNet's MinIterationCount)" >&2; exit 2; }

step="setup"
trap 'echo "ROUND FAILED during: $step (exit $?)" >&2' ERR

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
raw="$root/raw/$round/$vm"
work="$root/work/$round/$vm"
src="$work/src"
bin="$work/bin"

# --- 0. arguments, checked before anything is written (review-T12 Minors 1 and 3) ---------------------------
step="check the arguments"
sha="$(git -C "$repo" rev-parse --verify --quiet "$commit_arg^{commit}")" || {
    echo "error: commit $commit_arg is not in $repo" >&2; exit 1; }
git -C "$repo" rev-parse --verify --quiet "v4.9.1^{commit}" > /dev/null || {
    echo "error: tag v4.9.1 is not in $repo (the control build needs it)" >&2; exit 1; }
for tool in benchmarks/tools/round.sh benchmarks/tools/round_runs.py; do
    git -C "$repo" show "$sha:$tool" 2>/dev/null | cmp -s - "$repo/$tool" || {
        echo "error: $repo/$tool differs from the copy in $sha; run round.sh from a checkout of the measured commit" >&2
        exit 1; }
done
if [[ -f "$filter_arg" ]]; then
    filter_file="$(cd "$(dirname "$filter_arg")" && pwd)/$(basename "$filter_arg")"
    filter_from="file $filter_file"
elif git -C "$repo" cat-file -e "$sha:$filter_arg" 2>/dev/null; then
    filter_file="$(mktemp)"
    trap 'rm -f "$filter_file"' EXIT              # the temporary copy never outlives the script (rereview Minor 5)
    git -C "$repo" show "$sha:$filter_arg" > "$filter_file"
    filter_from="$filter_arg at $sha"
else
    echo "error: filter file $filter_arg not found (current directory or commit $sha)" >&2
    exit 1
fi
python3 -I "$repo/benchmarks/tools/round_runs.py" --check-filters "$filter_file"
# Is the filter file the commit's own copy? Recorded, not enforced (a dry run uses a narrower filter).
filter_rel="$(realpath --relative-to="$repo" "$filter_file" 2>/dev/null || echo "$filter_arg")"
if [[ "$filter_from" == "$filter_arg at $sha" ]]; then
    filter_tie="the copy in $sha"
elif git -C "$repo" show "$sha:$filter_rel" 2>/dev/null | cmp -s - "$filter_file"; then
    filter_tie="same as $filter_rel in $sha"
else
    filter_tie="NOT the copy in $sha (no file $filter_rel there, or it differs)"
fi
repo_head="$(git -C "$repo" rev-parse HEAD)"
[[ -z "$(git -C "$repo" status --porcelain)" ]] || repo_head="$repo_head (uncommitted changes)"

if [[ $resume -eq 0 && -e "$raw" && -n "$(ls -A "$raw" 2>/dev/null)" ]]; then
    echo "error: $raw is not empty; a round never overwrites results (--resume continues it, or use another name)" >&2
    exit 1
fi
if [[ $dry -eq 1 ]]; then
    mode="DRY RUN (ab.py --smoke, $launches launches, job $job${max_iterations:+, max iterations $max_iterations}): a smoke check of the script, not evidence (D1)"
else
    mode="round (B4: $launches launches per side, job ${job:-default}${max_iterations:+, max iterations $max_iterations})"
fi
filter_sha="$(sha256sum "$filter_file" | cut -d ' ' -f 1)"
# --resume continues the same measurement only: the commit, the mode line (launches, job, iteration cap, --dry) and
# the filter file (SHA-256) of the first call (round_runs.py --check-resume, tested). Each kept result file is checked
# again in round_runs.py against its glob, folders and binaries.
if [[ $resume -eq 1 && -s "$raw/machine.txt" ]]; then
    python3 -I "$repo/benchmarks/tools/round_runs.py" --check-resume "$raw/machine.txt" --commit "$sha" \
        --mode "$mode" --filter-sha "$filter_sha" || exit 1
fi
mkdir -p "$raw" "$raw/checks"

# --- 1. machine record -------------------------------------------------------------------------------------
step="machine record"
{
    if [[ $resume -eq 1 && -s "$raw/machine.txt" ]]; then
        echo
        echo "== resumed: $(date -u +%Y-%m-%dT%H:%M:%SZ) (mode: $mode)"
    else
        echo "round: $round"
        echo "vm: $vm"
        echo "started: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
        echo "mode: $mode"
        echo "commit: $sha (requested as $commit_arg)"
        echo "repository: $repo (HEAD $repo_head; round.sh and round_runs.py are the commit's copies)"
        echo "filter file: $filter_from, $filter_tie, sha256 $filter_sha"
        echo "launches: $launches"
        echo "job: ${job:-BenchmarkDotNet default}"
        echo "max iterations: ${max_iterations:-BenchmarkDotNet default}"
        echo
        echo "== uname -a"; uname -a
        echo; echo "== /etc/os-release"; cat /etc/os-release 2>/dev/null || echo "(none)"
        echo; echo "== lscpu"; lscpu
        echo; echo "== nproc"; nproc
        echo; echo "== free -m"; free -m
        echo; echo "== df -h $root"; df -h "$root"
        echo; echo "== dotnet --info"; dotnet --info
        echo; echo "== python3 --version"; python3 --version
        echo; echo "== git --version"; git --version
        echo; echo "== native toolchain (Native AOT publish)"; clang --version 2>/dev/null || gcc --version 2>/dev/null \
            || echo "(no clang or gcc on PATH: the AOT publish will fail)"
    fi
} >> "$raw/machine.txt" 2>&1
echo "machine record: $raw/machine.txt"
cp "$filter_file" "$raw/filters.txt"

# --- 2. clean clone at <commit> ----------------------------------------------------------------------------
step="clone $sha"
if [[ $resume -eq 1 && -d "$src/.git" && "$(git -C "$src" rev-parse HEAD 2>/dev/null)" == "$sha" \
      && -z "$(git -C "$src" status --porcelain)" ]]; then
    echo "resume: clone kept ($src)"
else
    rm -rf "$work"
    mkdir -p "$work"
    git clone --quiet --no-checkout "$repo" "$src"
    git -C "$src" checkout --quiet --detach "$sha"
fi

# --- 3. builds, one at a time ------------------------------------------------------------------------------
built=1
for name in net10 net11-ra-on net11-ra-off pkg-net10 pkg-net11 control-net10 control-net11-ra-on control-net11-ra-off \
            aot/net10 aot/net11-ra-on aot/net11-ra-off aot/pkg-net10 aot/pkg-net11; do
    [[ -f "$bin/$name/variant.txt" ]] || built=0
done
if [[ $resume -eq 1 && $built -eq 1 ]]; then
    echo "resume: builds kept ($bin); the checks below run again"
else
    step="build-variants.sh"
    echo "building the 4.10 configurations (log: $raw/build.log)"
    "$src/benchmarks/tools/build-variants.sh" "$bin" >> "$raw/build.log" 2>&1
    step="build-baselines.sh"
    echo "building the 4.9.1 package and control folders (log: $raw/build.log)"
    "$src/benchmarks/tools/build-baselines.sh" "$bin" >> "$raw/build.log" 2>&1
    step="build-aot-variants.sh"
    echo "publishing the Native AOT harness folders (log: $raw/build.log)"
    "$src/benchmarks/tools/build-aot-variants.sh" "$bin/aot" >> "$raw/build.log" 2>&1
fi
# After every build, the AOT publishes included: no build may leave a change in the clone.
if [[ -n "$(git -C "$src" status --porcelain)" ]]; then
    echo "error: the clone has uncommitted changes after the builds" >&2
    exit 1
fi

# --- 4. checks before any measurement (run again on --resume; they replace variants.txt and checks/) --------
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
declare -A library_sha
verify="$bin/.tools/verify-variant/verify-variant.dll"
checks_cwd="$work/checks-cwd"
mkdir -p "$checks_cwd"
rm -f "$raw/checks/"*

mismatch() {
    echo "VARIANT MISMATCH $1" >&2
    echo "  $2" >&2
    exit 1
}

: > "$raw/variants.txt"
for entry in "${folders[@]}"; do
    IFS='|' read -r name line major <<< "$entry"
    dir="$bin/$name"
    step="check $name"
    [[ -f "$dir/ModernLibs.FileHelpers.Benchmarks.dll" ]] || mismatch "$dir" "no benchmark build in the folder"
    actual="$(dotnet "$verify" "$dir/ModernLibs.FileHelpers.dll")" || mismatch "$dir" "verify-variant failed"
    [[ "$actual" == "$line" ]] || mismatch "$dir" "verify-variant: '$actual', expected '$line'"
    [[ -f "$dir/variant.txt" && "$(head -n 1 "$dir/variant.txt")" == "$name: $line" ]] \
        || mismatch "$dir" "variant.txt does not start with '$name: $line'"
    # Start-up lines of a process started from the folder (it lists nothing and exits 1: no benchmark matches).
    startup="$(cd "$checks_cwd" && dotnet "$dir/ModernLibs.FileHelpers.Benchmarks.dll" --list flat \
        --filter 'T12.Round.NoSuchBenchmark' 2>&1 | grep '^// ModernLibs\|^// Runtime' || true)"
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

# AOT folders: build-aot-variants.sh already checked each publish; round_runs.py --check-aot checks the folders that
# will be measured (tested in test_round_runs.py), prints "VARIANT MISMATCH <dir>" and exits 1 on a mismatch, and
# writes their variants.txt blocks.
step="check aot folders"
python3 -I "$src/benchmarks/tools/round_runs.py" --check-aot "$bin/aot" --checks-cwd "$checks_cwd" >> "$raw/variants.txt" \
    || exit 1

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

# --- 5. A/B runs (round_runs.py; review-T12 Important 1: a failed run is recorded and the round goes on) ------
step="A/B runs"
runs_args=(--raw "$raw" --bin "$bin" --src "$src" --filters "$raw/filters.txt" --launches "$launches"
           --checks-cwd "$checks_cwd")
[[ $dry -eq 1 ]] && runs_args+=(--dry)
[[ $resume -eq 1 ]] && runs_args+=(--resume)
[[ -n "$job" ]] && runs_args+=(--job "$job")
[[ -n "$max_iterations" ]] && runs_args+=(--max-iterations "$max_iterations")
code=0
python3 -I "$src/benchmarks/tools/round_runs.py" "${runs_args[@]}" || code=$?
echo "finished: $(date -u +%Y-%m-%dT%H:%M:%SZ) (exit $code)" >> "$raw/machine.txt"
exit "$code"
