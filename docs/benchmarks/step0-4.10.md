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
| Iteration cap | `<none, or --max-iterations N (BenchmarkDotNet maxIterationCount), as the mode cell of the machine table shows>` |
| Date | `<UTC start and end per VM>` |
| Failed and resumed runs | `<none, or the runs listed in failed.txt and whether round.sh --resume finished them>` |

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
library targets `net10.0;net11.0` with the runtime-async switch of 4.10. No source file of the tag is changed, and the
control keeps the tag's `LangVersion 14` (4.10 compiles with 15): the control measures the SDK 11 compiler on the 4.9.1
source as it was written, not a language-version change. Before
any measurement, `round.sh` checks every folder: `verify-variant` and the start-up lines of a process started from it
match the folder's name, its library DLL differs from every other folder's (the two package folders hold the package's
own DLL), and `--check-competitors` and `--check-prototypes` exit 0.

A/B pairs (`ab.py`, A and B launched alternately, one benchmark method per process):

| Pair | A | B | Purpose |
|---|---|---|---|
| `free-gains` | `pkg-net10` | `pkg-net11` | Free gains of .NET 11 |
| `sdk-net10` | `pkg-net10` | `control-net10` | Compiler and SDK effect on .NET 10 |
| `sdk-net11` | `pkg-net11` | `control-net11-ra-on` | Compiler and SDK effect on .NET 11 (recompiled for `net11.0`); for async scenarios it includes runtime async, which `ra-control` separates |
| `ra-control` | `control-net11-ra-off` | `control-net11-ra-on` | Runtime async on the 4.9.1 source |
| `v410-net10` | `pkg-net10` | `net10` | 4.10 build against the package; R6 and G2 on `net10` |
| `v410-net11` | `pkg-net11` | `net11-ra-on` | R6 and G2 on `net11-ra-on`, `R6_Current` against the package |
| `ra` | `net11-ra-off` | `net11-ra-on` | Runtime async, all scenarios (D6) |
| `runtime-410` | `net10` | `net11-ra-on` | Component benchmarks on both runtimes (coding-pattern choices) |
| `aot-v410-net10` | `aot/pkg-net10` | `aot/net10` | Native AOT read, 4.9.1 package against 4.10, .NET 10 |
| `aot-v410-net11` | `aot/pkg-net11` | `aot/net11-ra-on` | The same on .NET 11, runtime async on |
| `aot-v410-net11-ra-off` | `aot/pkg-net11` | `aot/net11-ra-off` | The same on .NET 11, runtime async off |
| `aot-ra` | `aot/net11-ra-off` | `aot/net11-ra-on` | Runtime async under AOT (synchronous read: A/A) |

The `aot-*` pairs run the Native AOT harness (`ab.py --aot`) on the folders under `aot/`; see "AOT read".

**Rule B4.** A change counts when the 99.9 % bootstrap interval of the ratio of medians excludes 1.0 and the median
ratio differs from 1.0 by at least 3 %. A claim holds only when it holds on at least two VMs, one of them the x64 AVX2
reference VM. Ratios are B/A: below 1.0 the second build is faster.

**How to read the tables.** A time is the median of the per-launch medians of that build and case on that VM. In the
scenario tables, the `4.9.1 pkg` column shows the package on .NET 10 and on .NET 11 (`x / y`); the package has no
runtime-async switch, so its .NET 11 number stands against both `net11` columns (design 3.5). `on/off` is
`net11-ra-on` / `net11-ra-off` from the `ra` pair, with its B4 verdict. A verdict marked `*` comes from a run that is
not B4 and is no result.

## Machines

The table `round-report.py` prints (paste it as it stands):

| VM | CPU | Architecture | Logical CPUs | SIMD flags | OS | Kernel | SDK | Runtimes | Commit | Mode |
|---|---|---|---|---|---|---|---|---|---|---|

Role and size of each VM (from the round request; the report builder does not know them):

| VM | Role | Azure size |
|---|---|---|
| | x64 AVX2, reference | |
| | x64 AVX-512 | |
| | Arm64 | |

The full records (`lscpu`, `uname -a`, `df -h`, `dotnet --info`, the native toolchain) are in
`raw/<round-name>/<vm>/machine.txt`.

## Free gains of .NET 11

4.9.1 package on .NET 10 against the same package on .NET 11 (pair `free-gains`), for every scenario a 4.9.1 build has
(design 3.1: numbers for 4.9.1 on both runtimes). The baseline against which every 4.10 claim on .NET 11 is read. One
table per VM. The last two columns are Gen0 / Gen1 / Gen2 (collections per 1,000 operations; `-` is zero) and the bytes
allocated per operation on each runtime (design 3.1 B1).

| Scenario | 4.9.1 on .NET 10 | 4.9.1 on .NET 11 | 11/10 [99.9 % CI] | verdict | Gen0 / Gen1 / Gen2; allocated, 4.9.1 on .NET 10 | Gen0 / Gen1 / Gen2; allocated, 4.9.1 on .NET 11 |
|---|---|---|---|---|---|---|

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
builds leave them out (`FILEHELPERS_BASELINE`), so they run only in the pair `ra` and their `4.9.1 pkg` and `net10`
cells stay empty. `Engine_WriteStreamAsync_Null_SourceThrows` is also in the exception-path table.

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### Exception path

A conversion error in every 100th row with `ErrorMode.SaveAndContinue` (`Engine_ReadAsync_Path_ExceptionEvery100`),
and `WriteStreamAsync` from a source that throws after 3 records, `Rows / 100` calls per invocation
(`Engine_WriteStreamAsync_Null_SourceThrows`, 4.10 builds only).

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

### AOT read

Design 3.1 (C2): the 25-column parse-and-discard read under Native AOT, on each runtime. BenchmarkDotNet cannot run an
AOT build, so this is the timing harness of T47 (`benchmarks/ModernLibs.FileHelpers.Benchmarks.Aot`, see
BENCHMARKS.md, "Native AOT timing harness"). It does the work of `DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard`
with the same record type and the same dataset, at 100,000 rows: per launch 5 untimed and 20 timed reads, and one
sample is the median of the 20. Every launch first checks that both builds of the pair read the same values (a
checksum over every field), and times nothing otherwise. `ab.py --aot` interleaves the launches with rule B4, as for
the other tables.

AOT folders (`round.sh` publishes them with `build-aot-variants.sh` into `<bin>/aot/`): `pkg-net10` and `pkg-net11`
(the 4.9.1 package, harness on .NET 10 and on .NET 11), `net10`, `net11-ra-on` and `net11-ra-off` (the 4.10 commit).
Pairs: `aot-v410-net10` (`pkg-net10` -> `net10`), `aot-v410-net11` (`pkg-net11` -> `net11-ra-on`),
`aot-v410-net11-ra-off` (`pkg-net11` -> `net11-ra-off`) and `aot-ra` (`net11-ra-off` -> `net11-ra-on`; the read is
synchronous, so this is an A/A check under AOT).

**No AOT control build.** There is no AOT build of the 4.9.1 source with SDK 11, so each 4.9.1 -> 4.10 ratio under
AOT includes the compiler and SDK effect (design 2.5 item 9) as well as the code change: read it as what a consumer
gets, not as the effect of the 4.10 code. Also: the harness forces a full collection before every timed read
(BenchmarkDotNet does so between iterations), and its x64 build does not assume AVX2 (no `IlcInstructionSet`), so its
times are not comparable with the BenchmarkDotNet tables; compare AOT only with AOT.

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|

4.9.1 package against 4.10 under AOT, per runtime (one table per pair and VM):

| Scenario | 4.9.1 pkg (AOT) | 4.10 (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|

The Native AOT smoke test (T04, `tests/ModernLibs.FileHelpers.Aot.Tests`) checks correctness under AOT and measures no
time.

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

**Capped rounds (R6 and G2).** An iteration cap (`--max-iterations`) can turn a `faster` verdict into `no-change`,
never the reverse. If a capped round shows a gate ratio under its limit (R6 0.94, G2 0.90) without the verdict
`faster` on a VM, the two gate lines are rerun uncapped on that VM under a second round name before the VMs are deleted
(`round.sh <round-name>-gates <sha> benchmarks/tools/filters-gates.txt --vm <vm-name>`, about 1 to 6 h per VM: 2
pairs, 8 cases, 14 launches, about 6 h when every case runs the 100 iterations of BenchmarkDotNet and about 1 h when
they stop near 20), and the
gate is read from that run. `round-report.py` prints a note under the G2 table when this applies.

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

**The prototype does not include P5.** `round-report.py` prints this note under the G2 table: `G2_SingleSpan` still
copies each record from the engine's record buffer into the result (it does not format straight into the
`PooledStringWriter` chunk), so it measures G2 without P5. A result just under 10 % understates what Phase 3 (G2, G3
and P5 together) can give; the decision should say so. Only the 100K row is a gate row.

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

| Benchmark | Build | 10K rows: ns per row | 100K rows: ns per row | 100K/10K | 10K: Gen0 / Gen1 / Gen2; allocated | 100K: Gen0 / Gen1 / Gen2; allocated |
|---|---|---|---|---|---|---|

The last two columns (Gen0, Gen1, Gen2 per 1,000 operations and bytes allocated per row) compare `net10` with
`net11-ra-on` and the 4.9.1 package (design 3.1 B1); a growth in Gen1 or Gen2 collections from 10K to 100K is the first
thing to check for the slope.

Explanation: `<to be written from the numbers; not a guess>`.

## GC and allocation

Gen0, Gen1 and Gen2 (collections per 1,000 operations) and bytes allocated per operation of every BenchmarkDotNet case
and build (design 3.1 B1), from the BenchmarkDotNet reports in the `*.runs` folders; the median over the launches.

| Scenario | Build | Gen0 | Gen1 | Gen2 | Allocated per operation |
|---|---|---|---|---|---|

## Findings F3 to F6

Design 3.6 lists four smaller cases where the round measured .NET 11.0.0 slower than .NET 10.0.15. T58 looked for the
cause of each with the method that found the cause of F1 (dotnet/runtime#137552): the steady-state Tier1 code of the
method on both runtimes, taken from the benchmark process started the way `ab.py` starts it (`dotnet <folder>/
ModernLibs.FileHelpers.Benchmarks.dll --filter <method> --artifacts <dir> --maxIterationCount 20`) with
`DOTNET_JitDisasm`, `DOTNET_JitDisasmSummary=1` and `DOTNET_JitStdOutFile`, then the same on .NET 11 with
`DOTNET_JitInlineBudget=5`, the value that restored F1.

Timings below come from the round's raw data (`step0-2026-11-12`, ratio .NET 11 / .NET 10 with its 99.9 % interval).
Code sizes and inline counts come from listings made on the developer machine (AMD Ryzen 9 5900X, AVX2, x86-64-v3);
they describe code, not time. Local timings are smoke and stay in the T58 task report; none of the four slowdowns
showed up there.

| Finding | Method | Tier1 code bytes .NET 10.0.15 / 11.0.0 | Callee no longer inlined on 11 | Effect of `DOTNET_JitInlineBudget` | Verdict: `ours` or `runtime` | Code shape that avoids it, or "none" | Hand-over |
|---|---|---|---|---|---|---|---|
| F3: `Format_GenericWriter` 1.292 [1.245, 1.342] `fh-amd`, 1.368 [1.365, 1.370] `fh-intel` | `ConverterCallShapeBenchmarks.Format_GenericWriter`; `SpanRecordWriter.Format<__Canon, int>` and `<__Canon, DateTime>` are inlined into it on both runtimes | 2308 / 2186 (`Format<__Canon, int>` alone 636 / 622, `Format<__Canon, DateTime>` alone 474 / 440) | None. The same callees on both runtimes; only the `CORINFO_HELP_STRCNS` calls on the throw paths are gone on 11. Inlinees with PGO data / single block / without PGO data: 34 / 52 / 13 and 32 / 53 / 12 | Budget 5 shrinks it to 584 bytes and stops inlining both `Format` instantiations, `Write` and `Commit`: the opposite of F1; slower in one local smoke launch | Neither shown: not reproduced locally; no inline-tree change, so not the F1 mechanism | None shown. `Format_WriterDirectCall` has no generic method between caller and converter; whether it keeps .NET 11 at the .NET 10 time is measured in the T19 round | T22: use the direct-call shape (`Format_WriterDirectCall`) |
| F4: `Format_DirectCall` 1.040 [1.015, 1.108] `fh-amd`, 1.132 [1.119, 1.146] `fh-intel` | `Format_DirectCall` with `WriteDirectCall` inlined | 1404 / 1354 | None. The same callees; inlinees 23 / 49 / 12 and 21 / 50 / 11 | Budget 5: 676 bytes, `Int32.TryFormat` and `DateTimeConverter.TryFormat` become calls; slower in one local smoke launch | Neither shown: not reproduced locally; no inline-tree change | None | See F3 |
| F5: `Scan_LoadUnsafe` (Quoted) 1.166 [1.023, 1.171] `fh-intel` only; `fh-amd` 1.011 (no change) | `VectorLoopShapeBenchmarks.Scan_LoadUnsafe` with `ScanLoadUnsafe` inlined | 439 / 439 (AVX2). AVX-512 is not on the developer machine: a Native AOT compile of a copy of `ScanLoadUnsafe` for `x86-64-v4` (not Tier1 code) gives 363 / 344 | None (one inlinee, the same on both) | None: the listing with budget 5 equals the default one | Neither shown: the case needs AVX-512, which only `fh-intel` has. The loop body is the same on both runtimes, in the AVX2 Tier1 code and in the AVX-512 AOT code (`vpcmpeqw` into mask registers, `korw`, `kmovw`); they differ only in alignment padding and one sign extension | None | T21 and T33: load form on .NET 11 is LoadUnsafe |
| F6: `ReadAllRows_Struct` 100K 1.056 [1.021, 1.080] `fh-amd` only (4.9.1 package, free-gains pair) | The generated `__FixedRecordStructDescriptor.Parse` (the slice code) and `ReadAllRows_Struct`, into which `ReadFile` and `ReadToArray` (the read loop) are inlined | `Parse` 1008 / 1000; `ReadAllRows_Struct` 2795 / 1614 | `Parse`: none (the same seven `string(ReadOnlySpan<char>)` calls; inlinees 29 / 28 / 5 on both). Read loop, among others: on 11 `ChunkedArraySink<T>.Add` (one call per record) is called instead of inlined, and `TextReaderLineSource.TryReadLine` is inlined instead of called; once per file, `SharedArrayPool<T>.Rent` (both instantiations) is called instead of inlined and the `RecordReadSession<T>` constructor is inlined instead of called | Budget 5: `Parse` unchanged (1000); `ReadToArray` is no longer inlined into `ReadAllRows_Struct` and inlines `Add` again | Neither shown: not reproduced locally. The round places it outside the per-row code (below) | None | T30: F6 is independent of the fixed-length slice code |

Verdict "open": the cause is not found. F3 and F4 are measured on both VMs (B4) but do not reproduce on the developer
machine, and their inline trees are the same on both runtimes, so the F1 mechanism is excluded. F5 is measured on
`fh-intel` only. Closing them needs the Tier1 listings from the VMs, taken in the T19 round with the same call
(`DOTNET_JitDisasm`, `DOTNET_JitDisasmSummary=1`, `DOTNET_JitStdOutFile`, default and `DOTNET_JitInlineBudget=5`); T19
owns the verdict. Until then no finding is `ours` and none is `runtime`. The listing script that starts the process
as `ab.py` does with these variables is `benchmarks/tools/dis.sh` (committed by T59).

Why each hand-over line holds without a local reproduction:

- **T22.** Step 0 measured the direct call against today's helper at 0.454 (`fh-amd`) and 0.441 (`fh-intel`) on
  .NET 10 and 0.488 and 0.529 on .NET 11; the generic writer at 0.630 and 0.584 on .NET 10, 0.842 and 0.846 on .NET 11.
  The direct call is the faster shape on both runtimes and loses less on .NET 11. These ratios are for the plain-span
  shape; the writer shape has no round data yet. T22's condition for the alternative, a
  `Format<TConverter, TValue>` shape whose Tier1 code on .NET 11 equals the direct call's, is not met: 2186 bytes and
  two generic instantiations against 1354 bytes. `Format_WriterDirectCall` (the frozen `FreeSpan` / `Advance` / `Grow`
  shape over an abstract buffer) has the same callees on both runtimes (1931 / 1882 bytes). Its span and position stay
  in stack memory in the Tier1 code (98 stack operands against 39 in `Format_DirectCall`), because the non-inlined
  growth methods take the writer's address. In a local smoke run it ran at the level of `Format_GenericWriter`, not of
  `Format_DirectCall`; T19 decides, by the reopening rule of T22 step 1.
- **T21 and T33.** Inside the .NET 11 build of the round the span form is never faster than `LoadUnsafe` by B4:
  `Scan_SpanCreate` / `Scan_LoadUnsafe` is 1.012 and 1.036 on `fh-amd` (Quoted, LongLines, no change), 0.932 [0.878,
  1.062] (Quoted, no change) and 1.121 [1.096, 1.168] (LongLines, slower) on `fh-intel`. The seven .NET 11 launches of
  `Scan_LoadUnsafe` Quoted on `fh-intel` fall in three groups (about 333, 321 and 292 µs), each steady inside its launch;
  the .NET 10 launches are all at 285 to 286 µs. The same two-level pattern is in the round on the second VM and
  dataset: `fh-amd`, `Scan_LoadUnsafe` LongLines, .NET 10 launches 171.3 to 172.2 µs (seven), .NET 11 launches 213.5,
  213.8, 171.8, 213.6, 214.3, 212.9, 213.9 µs. Over the two cases the slow level was hit in 13 of 14 .NET 11 launches
  and in 0 of 14 .NET 10 launches. With the same loop code on both runtimes, this fits a placement effect (code or
  data alignment) that depends on the runtime; it is not caused by the load form, and it is one more reason the
  verdict stays open.
  For T21 this means F5 gives no reason to avoid `LoadUnsafe` on .NET 11. The design's condition (it must beat the span
  form on .NET 11) is not met by Step 0 on `fh-amd`; T21's own round sets `UseUnsafeLoad`.
- **T30.** The same generated slice code (`line.Slice(o, Math.Min(w, line.Length - o))`, seven fields) runs in
  `ReadAllRows_Class` and in the class parse-and-discard read, and neither moved on .NET 11 (100K: 0.994 and 0.983 on
  `fh-amd`, 0.993 and 0.994 on `fh-intel`). `ReadAllRows_Struct` itself shows no change at 10K rows (1.002 `fh-amd`,
  0.957 `fh-intel`) and none on `fh-intel` at 100K (1.002); per-row code would cost the same per row at 10K and 100K.
  At 100K rows the read keeps 700,000 strings and a large struct array alive: Gen0 / Gen1 / Gen2 are 3187.5 / 3125 /
  1000 per 1,000 operations and 41.82 MB are allocated per operation, the same on both runtimes, and that part of the
  time (about 36 of the 48 ms; the parse-and-discard read takes about 12 ms) is where the 2.7 ms difference has room.

No verdict is `runtime`, so no comment for dotnet/runtime#137552 is required; an optional note on the F6 inline shift
is in the T58 report, not posted. The listings are kept outside the repository (T58 report).

## Not run

Globs of the filter file that selected nothing in one build of a pair (from `not-run.txt`), and methods that `ab.py`
found in one build of a pair only (`only_in_a`, `only_in_b`).

| Pair | Group | Glob | Reason |
|---|---|---|---|

## Failed

`ab.py` runs that failed (from `failed.txt`); the round went on without them. `round.sh ... --resume` reruns them and
keeps every finished result. A final report has this table empty.

| Pair | Group | Glob | Log |
|---|---|---|---|

## Reproducing

On each VM (see [raw/README.md](raw/README.md) for the layout of the raw files):

```bash
# uncapped:
benchmarks/tools/round.sh <round-name> <sha> benchmarks/tools/filters-step0.txt --vm <vm-name>
# or capped at 20 iterations:
benchmarks/tools/round.sh <round-name> <sha> benchmarks/tools/filters-step0.txt --vm <vm-name> --max-iterations 20
# after "ROUND COMPLETE <n> result files, <m> FAILED" with m > 0: the same command with --resume
# capped round, gate ratio under its limit without "faster" (see Gate R6): the gate lines uncapped, second round name
benchmarks/tools/round.sh <round-name>-gates <sha> benchmarks/tools/filters-gates.txt --vm <vm-name>
python3 -I benchmarks/tools/round-report.py ~/fh-round/raw/<round-name> > section.md
```
