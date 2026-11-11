# Raw benchmark round files

Each Azure benchmark round of 4.10 (design 3.5) keeps its raw files here, so that every number in a report under
`docs/benchmarks/` can be checked against them. `benchmarks/tools/round.sh` writes them on each VM; the lead copies
them here after the round (design 3.5 step 4) and verifies the copy before the VMs are deleted.

## Layout

```text
raw/<round-name>/<vm>/
  machine.txt        round, VM, mode (round or DRY RUN), commit, launches, job, start and end time (UTC),
                     uname -a, /etc/os-release, lscpu, nproc, free -m, dotnet --info, python3 and git versions,
                     the native toolchain (clang or gcc) of the AOT publish
  variants.txt       per build folder: its variant.txt (verify-variant line, commit, SDK, library, SHA-256 of the
                     library DLL) and the start-up lines of a process started from it; for the AOT folders
                     (aot/...), their variant.txt (with RID and aot=true) and the --info lines of the executable
  filters.txt        the filter file the round ran (<group> <pairs> <glob> per line)
  commands.txt       every ab.py command, exactly as run
  timing.txt         pair, glob, start, end (UTC) and seconds of every ab.py run
  not-run.txt        globs that selected nothing in one build of a pair (pair, group, glob, reason)
  build.log          output of build-variants.sh, build-baselines.sh and build-aot-variants.sh
  checks/            output of --check-competitors and --check-prototypes per build folder
  <pair>/<line>-<group>-<glob>.json   ab.py result: samples, ratio, 99.9 % interval and verdict per case
  <pair>/<line>-<group>-<glob>.log    ab.py console output
  <pair>/<line>-<group>-<glob>.json.runs/   one folder per launch: console.log and BenchmarkDotNet's reports
                                            (for the aot-* pairs: the checksum run and each launch's output)
```

`<line>` is the line number in `filters.txt`. The `aot-*` pair folders hold `ab.py --aot` results (`"harness": "aot"`). The pairs and build folders are described in
[step0-4.10.md](../step0-4.10.md).

## Building the report

```bash
python3 -I benchmarks/tools/round-report.py docs/benchmarks/raw/<round-name> > section.md
```

`round-report.py` reads every VM folder, skips the `*.runs` folders and prints the tables of the report. A result file
marked `"b4": false` (a smoke run) is never a result; the report marks its verdicts with `*`.

## What is committed

The JSON results, logs and text files above are committed. The `*.runs` folders hold every launch's BenchmarkDotNet
output (HTML, CSV and Markdown tables besides the JSON); if they are too large for the repository, the lead archives
them (`tar -czf <vm>-runs.tar.gz */*.runs`) as a release asset or other artifact, and names it in the report. A dry
run (`round.sh --dry`) is a local smoke check and is never committed here (design D1).
