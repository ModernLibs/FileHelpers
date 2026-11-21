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
same command plus `--resume`: it keeps the finished result files and reruns only the missing ones. It refuses another
commit, mode or filter file than the first call recorded in `machine.txt`, and it runs again a result file that was made
for another glob, by other folders or from other binaries. A result file cut off by a kill is listed by
`round-report.py` under "Damaged results" and is run again by `--resume`.

## Rounds committed here

| Round | What | VMs | Archive (outside the repository) | SHA-256 |
|---|---|---|---|---|
| `step0-2026-11-12` | Step 0 (design 3.6) | fh-amd, fh-intel | `step0-fh-amd.tgz`; `step0-fh-intel.tgz` | `cd65925e9e76ad8b9b46d60362b7b758ec61fb16522f812badaf52a7cf7ffe38`; `8f7b5341345b0cda0cc6631e7c656a0b729ba0e9d91129af37ae4edeefd2928e` |
| `g1-2026-11-16` | T19 A/B round (`filters-g1.txt`) | fh-amd, fh-intel | `g1-fh-amd.tar.gz`; `g1-fh-intel.tar.gz`, and the archives of the next row | see the next row |
| `simd-2026-11-17`, `noavx2-2026-11-17` (fh-amd only), `recheck-2026-11-17`, `write-2026-11-17` | round 2 (T21/T25 and the recheck of the T19 findings) | fh-amd, fh-intel | `all-fh-amd-2026-11-20.tar.gz`; `all-fh-intel-2026-11-20.tar.gz` (every round of this table after Step 0, with the `*.runs` folders and the VMs' console logs) | `fcc39b4a13917fc1e9eb8a0a9daa859921f49c3a1292a7bdebb9a929ab672392`; `7ba95352469cd42424990c246a85f988262a125c546f854a1e957903c64662de` |
| `diagA1-2026-11-19` (fh-intel only), `diagAS-2026-11-19`, `diagFW-2026-11-19`, `diagAS2-2026-11-19`, `diagFW2-2026-11-19` | A/Bs of the async and fixed-width write diagnoses; `diagAS` and `diagFW` stopped at the checks (a harness defect, fixed by `0f55b0e`) and hold no result | fh-amd, fh-intel | as above | as above |
| `r3cmp-2026-11-19`, `r3q-2026-11-20` | library comparison (T50 step 2, `round.sh --compare`); the quoted line of `r3cmp` failed in every configuration (its glob took `FileHelpers_ParseAndDiscard_ScanOnly`) and `r3q` reran it at `1ad7b18` | fh-amd, fh-intel | as above | as above |

The `*.runs` folders are not committed; they are in the archives. A comparison round keeps, per launch, `console.log`
and BenchmarkDotNet's `*-report-full.json` (what `round-report.py --compare` reads). The archives are kept by the
owner, outside the repository.

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
