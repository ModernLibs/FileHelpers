#!/usr/bin/env bash
# Runs one benchmark method under the JIT disassembler (DOTNET_JitDisasm) and writes the listing to a file;
# T19 runs it on the round VMs to check the code the JIT made for the measured methods (set FH_DIS_ROOT to the folder with bin/<variant>).
# dis.sh <variant> <bdn-filter> <JitDisasm pattern> <tag> [VAR=value ...]
# Starts the benchmark process as ab.py does (dotnet <dll> --filter <m> --artifacts <dir> --maxIterationCount 20,
# cwd = the artifacts dir) with DOTNET_JitDisasm, DOTNET_JitDisasmSummary=1, DOTNET_JitStdOutFile.
set -euo pipefail
export DOTNET_ROOT=$HOME/.dotnet
variant=$1; filter=$2; pattern=$3; tag=$4; shift 4
root=${FH_DIS_ROOT:-$HOME/scratch/fh-t58}
bin=$root/bin/$variant
out=$root/dis
mkdir -p "$out"
art=$root/runs/$tag-$variant
rm -rf "$art"; mkdir -p "$art"
jit="$out/$tag-$variant.txt"
rm -f "$jit"
cd "$art"
env "$@" DOTNET_JitDisasm="$pattern" DOTNET_JitDisasmSummary=1 DOTNET_JitStdOutFile="$jit" \
    dotnet "$bin/ModernLibs.FileHelpers.Benchmarks.dll" --filter "$filter" --artifacts "$art" --maxIterationCount 20 \
    > "$art/console.log" 2>&1
echo "$tag $variant exit $? -> $jit ($(wc -l < "$jit") lines)"
grep -E "^\| " "$art"/results/*-report-github.md | tail -n +3 || true
