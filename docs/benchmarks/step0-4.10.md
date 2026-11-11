# Step 0 for 4.10: ModernLibs.FileHelpers 4.9.1 baseline, gates R6 and G2

**Status: template.** No round has run yet. The tables below are empty; they are filled from the output of
`benchmarks/tools/round-report.py` after the Azure round (T12 step 7), and every number must be traceable to the raw
files under [`raw/`](raw/README.md). Local numbers never go into this file (design D1).

## What the round measures

Step 0 measures 4.9.1 before any 4.10 performance work, separates three effects (runtime, compiler and SDK, our
code), and decides gates R6 and G2 (design 3.1 to 3.5).

| | |
|---|---|
| Round | `<round-name>` |
| Commit measured | `<sha>` (`release/4.10.0`) |
| Filter file | `benchmarks/tools/filters-step0.txt` at that commit |
| Launches | 7 per side and benchmark method (the B4 minimum of `ab.py`), BenchmarkDotNet default job |
| Date | `<UTC start and end per VM>` |

Builds (each a folder of `round.sh`; the benchmark code is the same in all, only the library differs):

| Folder | Library | Target | Runs on | Runtime async |
|---|---|---|---|---|
| `pkg-net10` | ModernLibs.FileHelpers 4.9.1 package from nuget.org | `net10.0` | .NET 10 | does not exist |
| `pkg-net11` | the same package (its `lib/net10.0`) | `net10.0` | .NET 11 | does not exist |
| `control-net10` | the `v4.9.1` source built with SDK 11.0.100 | `net10.0` | .NET 10 | does not exist |
| `control-net11-ra-on` | the `v4.9.1` source for `net11.0` | `net11.0` | .NET 11 | on |
| `control-net11-ra-off` | the same, runtime async off | `net11.0` | .NET 11 | off |
| `net10` | the 4.10 commit | `net10.0` | .NET 10 | does not exist |
| `net11-ra-on` | the 4.10 commit, what ships | `net11.0` | .NET 11 | on |
| `net11-ra-off` | the 4.10 commit, `-p:FileHelpersRuntimeAsync=false` | `net11.0` | .NET 11 | off |

The control source is the `v4.9.1` tag with `benchmarks/tools/control-v4.9.1.patch`: `global.json` names SDK 11 and the
library targets `net10.0;net11.0` with the runtime-async switch of 4.10. No source file of the tag is changed. Before
any measurement, `round.sh` checks every folder: `verify-variant` and the start-up lines of a process started from it
match the folder's name, its library DLL differs from every other folder's (the two package folders hold the package's
own DLL), and `--check-competitors` and `--check-prototypes` exit 0.

A/B pairs (`ab.py`, A and B launched alternately, one benchmark method per process):

| Pair | A | B | Purpose |
|---|---|---|---|
| `free-gains` | `pkg-net10` | `pkg-net11` | Free gains of .NET 11 |
| `sdk-net10` | `pkg-net10` | `control-net10` | Compiler and SDK effect on .NET 10 |
| `sdk-net11` | `pkg-net11` | `control-net11-ra-on` | Compiler and SDK effect on .NET 11 (recompiled for `net11.0`) |
| `ra-control` | `control-net11-ra-off` | `control-net11-ra-on` | Runtime async on the 4.9.1 source |
| `v410-net10` | `pkg-net10` | `net10` | 4.10 build against the package; R6 and G2 on `net10` |
| `v410-net11` | `pkg-net11` | `net11-ra-on` | R6 and G2 on `net11-ra-on`, `R6_Current` against the package |
| `ra` | `net11-ra-off` | `net11-ra-on` | Runtime async, all scenarios (D6) |
| `runtime-410` | `net10` | `net11-ra-on` | Component benchmarks on both runtimes (coding-pattern choices) |

**Rule B4.** A change counts when the 99.9 % bootstrap interval of the ratio of medians excludes 1.0 and the median
ratio differs from 1.0 by at least 3 %. A claim holds only when it holds on at least two VMs, one of them the x64 AVX2
reference VM. Ratios are B/A: below 1.0 the second build is faster.

**How to read the tables.** A time is the median of the per-launch medians of that build and case on that VM. In the
scenario tables, the `4.9.1 pkg` column shows the package on .NET 10 and on .NET 11 (`x / y`); the package has no
runtime-async switch, so its .NET 11 number stands against both `net11` columns (design 3.5). `on/off` is
`net11-ra-on` / `net11-ra-off` from the `ra` pair, with its B4 verdict. A verdict marked `*` comes from a run that is
not B4 and is no result.

## Machines

| VM | Role | Azure size | CPU | Architecture | Logical CPUs | SIMD flags | OS | Kernel | SDK | Runtimes |
|---|---|---|---|---|---|---|---|---|---|---|
| | x64 AVX2, reference | | | | | | | | | |
| | x64 AVX-512 | | | | | | | | | |
| | Arm64 | | | | | | | | | |

The full records (`lscpu`, `uname -a`, `dotnet --info`) are in `raw/<round-name>/<vm>/machine.txt`.

## Free gains of .NET 11

4.9.1 package on .NET 10 against the same package on .NET 11 (pair `free-gains`). The baseline against which every
4.10 claim on .NET 11 is read. One table per VM.

| Scenario | 4.9.1 on .NET 10 | 4.9.1 on .NET 11 | 11/10 [99.9 % CI] | verdict |
|---|---|---|---|---|

## Compiler and SDK effect

The published 4.9.1 package (built with SDK 10) against the same source built with SDK 11 (pairs `sdk-net10`,
`sdk-net11`), and runtime async on the 4.9.1 source (`ra-control`). One table per pair and VM.

| Scenario | 4.9.1 pkg | control (SDK 11) | control/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|

## Scenarios

One table per scenario group and VM. Rows key on the benchmark method and its parameters.

### 25-column read

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Quoted read

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Other plain reads (long lines, 150 columns)

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Fixed-width read

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Writes

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Async

Four methods of `EngineAsyncBenchmarks` use APIs that 4.9.1 does not have (T43, T46): `Engine_WriteStreamAsync_Null`,
`Engine_ReadAsync_Stream`, `Engine_ReadAsync_TextReader` and `Engine_WriteStreamAsync_Null_SourceThrows`. The 4.9.1
builds leave them out (`FILEHELPERS_BASELINE`), so their `4.9.1 pkg` cell stays empty and they run only in the pairs
`ra` and `runtime-410`. `Engine_WriteStreamAsync_Null_SourceThrows` is also in the exception-path table.

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Exception path

A conversion error in every 100th row with `ErrorMode.SaveAndContinue` (`Engine_ReadAsync_Path_ExceptionEvery100`),
and `WriteStreamAsync` from a source that throws after 3 records, `Rows / 100` calls per invocation
(`Engine_WriteStreamAsync_Null_SourceThrows`, 4.10 builds only).

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### AOT read

Not measured in Step 0. The Native AOT smoke test (T04, `tests/ModernLibs.FileHelpers.Aot.Tests`) publishes with
`PublishAot` for `net10.0` and `net11.0` and checks that reads and writes give the right results; it measures no time,
and no benchmark runs under AOT (the gate prototypes need reflection, and BenchmarkDotNet runs in-process). Design 3.1
(C2) asks for one AOT benchmark run of the 25-column read on each runtime; that run does not exist yet.

PLACEHOLDER, filled by T46/T47 integration: the AOT read timing harness of T47, its table and how the round runs it.

### Controls

Code that is the same in every build (Sylvan, CsvHelper, the span slicer of the fixed-width suite). Their ratios show
the noise of each pair.

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

## Gate R6

**Rule (design 3.2, ruling R32).** `R6_PrecomputedEnds` is at least 6 % faster than `R6_Current` on quoted
parse-and-discard at 100K rows (ratio `R6_PrecomputedEnds / R6_Current` at most 0.94 with the B4 verdict `faster`),
measured inside one 4.10 build, on the x64 AVX2 reference VM and on one more VM. `R6_PrecomputedEnds` is R6 alone (it
unquotes with today's emitted strip pass) and decides the gate. `R6_PrecomputedEnds_FastUnquote` is R6 with R3 (the
fastest correct unquote) and is informative; both ratios are reported.

| Build | Rows | R6_Current | R6_PrecomputedEnds | ratio [99.9 % CI] | gate | R6_PrecomputedEnds_FastUnquote | ratio [99.9 % CI] | at 6 % |
|---|---|---|---|---|---|---|---|---|

**R6_Current against the 4.9.1 package.** The gate compares two methods of one 4.10 build. "Faster than 4.9.1" holds
only if `R6_Current` measures what 4.9.1 does: the same VM runs `R6_Current` on the 4.9.1 package and on the 4.10 build,
interleaved (pairs `v410-net10`, `v410-net11`), next to the package's `QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard`
(the same work). If `R6_Current` of the 4.10 build differs from the package's by the B4 rule, the gate ratio is not the
ratio against 4.9.1, and the column `R6_PrecomputedEnds 4.10 / R6_Current pkg` gives that ratio directly.

| Pair | Rows | R6_Current 4.9.1 pkg | R6_Current 4.10 | 4.10/pkg [99.9 % CI] | verdict | R6_PrecomputedEnds 4.10 / R6_Current pkg [99.9 % CI] | quoted parse-and-discard, 4.9.1 pkg |
|---|---|---|---|---|---|---|---|

Decision: `<meets or misses, with the ratios of the two VMs>`. If `R6_PrecomputedEnds` and
`R6_PrecomputedEnds_FastUnquote` fall on different sides of 6 %, the report says so and the decision is the owner's.
If the gate fails, the generator emits the tokenized entry only for records with no `[FieldQuoted]` field and T20 is
not built.

## Gate G2

**Rule (design 3.2).** `G2_SingleSpan` makes `WriteString` of 100K 25-column records at least 10 % faster than
`G2_Current` (ratio at most 0.90 with the B4 verdict `faster`), inside one 4.10 build, on the reference VM and one more.

| Build | Rows | G2_Current | G2_SingleSpan | ratio [99.9 % CI] | gate (at least 10 % faster) |
|---|---|---|---|---|---|

**The prototype does not include P5.** `G2_SingleSpan` still copies each record from the engine's record buffer into
the result (it does not format straight into the `PooledStringWriter` chunk), so it measures G2 without P5. A result
just under 10 % understates what Phase 3 (G2, G3 and P5 together) can give; the decision should say so.

Decision: `<meets or misses, with the ratios of the two VMs>`. If the gate fails, `RecordWriter` and
`IRecordWriterDescriptor<T>` are not built, G3 and G4 use static helpers that take `IBufferWriter<char>`, and P5 is
dropped.

## Coding-pattern choices

Design 2.5 items 1, 2 and 3, each per runtime (`net10`, `net11-ra-on`, pair `runtime-410`) and per architecture (one
table per VM). Each row compares two methods of one build, measured in the same run.

| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |
|---|---|---|---|---|---|---|

1. Safe span vector loop (`Scan_SpanCreate`) against the `Unsafe` loop (`Scan_LoadUnsafe`).
2. Converter call shapes for the write path: `Format_GenericWriter` and `Format_DirectCall` against
   `Format_GenericHelper`.
3. The "any match first" block test (`Tokenizer_SinglePassVector_AnyMatchFirst`) against `Tokenizer_SinglePassVector`;
   expected to cost on Arm64.

Choice per item: `<pattern chosen, per runtime and architecture, with the numbers>`.

## Component benchmarks

B3: today's code against the candidates of R3, R4 and G2, per runtime: `Tokenizer_*` against
`Tokenizer_CountPlusIndexOf`, `Unquote_IndexOf` against `Unquote_Current` (`Values`: `All`, `DoubledOnly`),
`CreateString_Cached` against `CreateString_New`, `FormatRecord_SingleSpan` against `FormatRecord_Current`.

| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |
|---|---|---|---|---|---|---|

## Runtime async

Not a gate (D4). `net11-ra-on` against `net11-ra-off` for every scenario (pair `ra`), and the same on the 4.9.1 source
(`ra-control`). Every case that is slower with runtime async on by the B4 rule is listed here for the owner, with
whether it is slower than the largest control deviation of the same pair ("beyond control noise").

| Slower with runtime async on | on/off [99.9 % CI] | verdict | beyond control noise |
|---|---|---|---|

## Slope from 10K to 100K rows

Time per row at 10K and at 100K rows, per build, for every scenario measured at both sizes; CsvHelper
(`DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard`) is the reference whose growth is smaller (design 3.1).

| Benchmark | Build | 10K rows: ns per row | 100K rows: ns per row | 100K/10K |
|---|---|---|---|---|

Explanation: `<to be written from the numbers; not a guess>`.

## Not run

Globs of the filter file that selected nothing in one build of a pair (from `not-run.txt`).

| Pair | Group | Glob | Reason |
|---|---|---|---|

## Reproducing

On each VM (see [raw/README.md](raw/README.md) for the layout of the raw files):

```bash
benchmarks/tools/round.sh <round-name> <sha> benchmarks/tools/filters-step0.txt --vm <vm-name>
python3 -I benchmarks/tools/round-report.py ~/fh-round/raw/<round-name> > section.md
```
