# Raw benchmark round files

Each Azure benchmark round of 4.10 (design 3.5) keeps its raw files here, so that every number in a report under
`docs/benchmarks/` can be checked against them. `benchmarks/tools/round.sh` writes them on each VM; the lead copies
them here after the round (design 3.5 step 4) and verifies the copy before the VMs are deleted.

## Layout

```text
raw/<round-name>/<vm>/
  machine.txt        round, VM, mode (round or DRY RUN, with the job and the iteration cap), commit, the running
                     repository's HEAD, the filter file (source, whether it is the commit's copy, SHA-256), launches,
                     job, max iterations, start and end time (UTC), uname -a, /etc/os-release, lscpu, nproc,
                     free -m, df -h, dotnet --info, python3 and git versions, the native toolchain (clang or gcc) of
                     the AOT publish; a "resumed" block for every round.sh --resume
  variants.txt       per build folder: its variant.txt (verify-variant line, commit, SDK, library, SHA-256 of the
                     library DLL) and the start-up lines of a process started from it; for the AOT folders
                     (aot/...), their variant.txt (with RID and aot=true) and the --info lines of the executable
  filters.txt        the filter file the round ran (<group> <pairs> <glob> per line)
  commands.txt       every ab.py command, exactly as run ("# started" and "# resumed" lines separate the calls)
  timing.txt         pair, glob, start, end (UTC), seconds and outcome (ok or FAILED exit N) of every ab.py run
  not-run.txt        globs that selected nothing in one build of a pair (pair, group, glob, reason), last call
  failed.txt         ab.py runs that failed (pair, group, glob, log); the round went on without them; last call
  build.log          output of build-variants.sh, build-baselines.sh and build-aot-variants.sh
  checks/            output of --check-competitors and --check-prototypes per build folder
  <pair>/<line>-<group>-<glob>.json   ab.py result: samples, ratio, 99.9 % interval and verdict per case
  <pair>/<line>-<group>-<glob>.log    ab.py console output
  <pair>/<line>-<group>-<glob>.json.runs/   one folder per launch: console.log and BenchmarkDotNet's reports
                                            (for the aot-* pairs: the checksum run and each launch's output)
```

`<line>` is the line number in `filters.txt`. The `aot-*` pair folders hold `ab.py --aot` results
(`"harness": "aot"`). The pairs and build folders are described in [step0-4.10.md](../step0-4.10.md).

A round that ends with `ROUND COMPLETE <n> result files, <m> FAILED` and m > 0 is continued on the same VM with the
same command plus `--resume`: it keeps the finished result files and reruns only the missing ones.

## Building the report

```bash
python3 -I benchmarks/tools/round-report.py docs/benchmarks/raw/<round-name> > section.md
```

`round-report.py` reads every VM folder. It takes the times and ratios from the result files and the Gen0, Gen1, Gen2
and allocation columns (design 3.1 B1) from the BenchmarkDotNet reports in the `*.runs` folders. A result file marked
`"b4": false` (a smoke run) is never a result; the report marks its verdicts with `*`.

## What is committed

The JSON results, logs and text files above are committed. **The `*.runs` folders are kept until the report of the
round has been reviewed**: the GC and allocation columns are read from them, and a reviewer checks those numbers
against them. They hold every launch's BenchmarkDotNet output (HTML, CSV and Markdown tables besides the JSON); if they
are too large for the repository, the lead archives them (`tar -czf <vm>-runs.tar.gz */*.runs`) as a release asset or
other artifact, names it in the report, and removes them only after the review. A dry run (`round.sh --dry`) is a
local smoke check and is never committed here (design D1).
