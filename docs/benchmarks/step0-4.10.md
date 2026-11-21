# Step 0 for 4.10: ModernLibs.FileHelpers 4.9.1 baseline, gates R6 and G2

**Status: final.** The tables below are the output of `benchmarks/tools/round-report.py` on the round
`step0-2026-11-12` (T50 step 1). Every number is traceable to the raw files under
[`raw/step0-2026-11-12/`](raw/README.md); the Gen0 / Gen1 / Gen2 and allocation columns come from the `*.runs` folders,
which are in the archives `raw/README.md` lists and are not committed. Local numbers never go into this file (design
D1). The 4.10 rounds that came after Step 0 are in [v4.10.0.md](v4.10.0.md).

## What the round measures

Step 0 measures 4.9.1 before any 4.10 performance work, separates three effects (runtime, compiler and SDK, our
code), and decides gates R6 and G2 (design 3.1 to 3.5).

| | |
|---|---|
| Round | `step0-2026-11-12` |
| Commit measured | `fe784c30819ea342cd9b42270c0a5c8cb367720b` (`release/4.10.0`) |
| Filter file | `benchmarks/tools/filters-step0.txt` at that commit (sha256 `556fec62df21ccc7e64de50f5aedf09922ff4bb783f2aa9ad2429966f9ea6a64`, the same file on both VMs) |
| Launches | 7 per side and benchmark method (the B4 minimum of `ab.py`), BenchmarkDotNet default job |
| Iteration cap | `--max-iterations 20` (BenchmarkDotNet `maxIterationCount`), as the mode cell of the machine table shows; no row cap (`Rows=1000000` ran) |
| Date | `fh-amd`: 2026-11-12T09:45:39Z to 2026-11-13T09:42:10Z; `fh-intel`: 2026-11-12T09:45:38Z to 2026-11-13T09:02:54Z (UTC, `machine.txt`) |
| Failed and resumed runs | none: `failed.txt` and `not-run.txt` are empty on both VMs, and no `round.sh --resume` ran; 120 result files per VM (116 BenchmarkDotNet, 4 AOT) |

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
not B4 and is no result. `net11-ra-on` is what ships.

**The 4.10 commit of Step 0.** `fe784c3` is the end of part 1 (4.9.1 code plus the benchmark tooling); the `net10` and
`net11-*` columns of this page are therefore close to 4.9.1, not the 4.10 release. The 4.10 code is measured in
[v4.10.0.md](v4.10.0.md).

## Machines

The table `round-report.py` prints:

| VM | CPU | Architecture | Logical CPUs | SIMD flags | OS | Kernel | SDK | Runtimes | Commit | Mode |
|---|---|---|---|---|---|---|---|---|---|---|
| fh-amd | AMD EPYC 7763 64-Core Processor | x86_64 | 4 | avx2 | Ubuntu 24.04.5 LTS | 7.0.0-1017-azure | 11.0.100 | 10.0.15, 11.0.0 | fe784c30819e | round (B4: 7 launches per side, job default, max iterations 20) |
| fh-intel | Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz | x86_64 | 4 | avx2, avx512f | Ubuntu 24.04.5 LTS | 7.0.0-1017-azure | 11.0.100 | 10.0.15, 11.0.0 | fe784c30819e | round (B4: 7 launches per side, job default, max iterations 20) |

Role and size of each VM (from the round request; the report builder does not know them):

| VM | Role | Azure size |
|---|---|---|
| `fh-amd` | x64 AVX2, reference | `Standard_D4ads_v5`, Poland Central |
| `fh-intel` | x64 AVX-512 | `Standard_D4ds_v5`, Poland Central |
| - | Arm64 | not in the round (waiver D9, Step 0 only) |

The full records (`lscpu`, `uname -a`, `df -h`, `dotnet --info`, the native toolchain) are in
`raw/step0-2026-11-12/<vm>/machine.txt`. Every BenchmarkDotNet launch of the round ran on the CPU of this table (the
`ProcessorName` of each launch's report).

## Gate summary

| VM | Build | R6_PrecomputedEnds / R6_Current (gate 0.94) | R6_PrecomputedEnds_FastUnquote / R6_Current (informative) | G2_SingleSpan / G2_Current (gate 0.90) | B4 |
|---|---|---|---|---|---|
| fh-amd | net10 | 0.903 meets | 0.762 meets | 0.486 meets | yes |
| fh-amd | net11-ra-on | 0.840 meets | 0.684 meets | 0.520 meets | yes |
| fh-intel | net10 | 0.848 meets | 0.718 meets | 0.562 meets | yes |
| fh-intel | net11-ra-on | 0.815 meets | 0.677 meets | 0.572 meets | yes |

## Free gains of .NET 11

4.9.1 package on .NET 10 against the same package on .NET 11 (pair `free-gains`), for every scenario a 4.9.1 build has
(design 3.1: numbers for 4.9.1 on both runtimes). The baseline against which every 4.10 claim on .NET 11 is read. One
table per VM. The last two columns are Gen0 / Gen1 / Gen2 (collections per 1,000 operations; `-` is zero) and the bytes
allocated per operation on each runtime (design 3.1 B1).

### `fh-amd`

| Scenario | 4.9.1 on .NET 10 | 4.9.1 on .NET 11 | 11/10 [99.9 % CI] | verdict | Gen0 / Gen1 / Gen2; allocated, 4.9.1 on .NET 10 | Gen0 / Gen1 / Gen2; allocated, 4.9.1 on .NET 11 |
|---|---|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 6.600 ms | 6.449 ms | 0.977 [0.954, 1.027] | no-change | 460.9 / 85.94 / -; 7.396 MB | 460.9 / 85.94 / -; 7.396 MB |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 67.22 ms | 65.85 ms | 0.980 [0.937, 1.015] | no-change | 4625 / 125 / -; 74.89 MB | 4625 / 125 / -; 74.89 MB |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 670.1 ms | 653.5 ms | 0.975 [0.950, 1.004] | no-change | 4.7e+04 / - / -; 763.6 MB | 4.7e+04 / - / -; 763.6 MB |
| OptimizedParseBenchmarks.ReadFile (Rows 10K) | 7.816 ms | 7.641 ms | 0.978 [0.940, 1.007] | no-change | 453.1 / 421.9 / -; 7.473 MB | 453.1 / 421.9 / -; 7.472 MB |
| OptimizedParseBenchmarks.ReadFile (Rows 100K) | 160.5 ms | 160.5 ms | 1.000 [0.954, 1.030] | no-change | 6000 / 5750 / 1500; 75.67 MB | 6333 / 6000 / 1667; 75.67 MB |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.626 ms | 2.561 ms | 0.975 [0.932, 1.015] | no-change | 140.6 / 19.53 / -; 2.246 MB | 140.6 / 19.53 / -; 2.246 MB |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.57 ms | 25.73 ms | 0.969 [0.935, 1.024] | no-change | 1375 / 156.2 / -; 22.03 MB | 1375 / 156.2 / -; 22.03 MB |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 10K) | 2.799 ms | 2.709 ms | 0.968 [0.870, 0.988] | faster | 144.5 / 125 / -; 2.322 MB | 144.5 / 121.1 / -; 2.322 MB |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 100K) | 47.49 ms | 47.43 ms | 0.999 [0.932, 1.023] | no-change | 1875 / 1812 / 625; 22.80 MB | 1875 / 1812 / 625; 22.80 MB |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.459 ms | 2.451 ms | 0.997 [0.934, 1.041] | no-change | 140.6 / 19.53 / -; 2.245 MB | 140.6 / 19.53 / -; 2.245 MB |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 24.92 ms | 24.64 ms | 0.989 [0.934, 1.041] | no-change | 1375 / 156.2 / -; 22.02 MB | 1375 / 156.2 / -; 22.02 MB |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.569 ms | 1.507 ms | 0.961 [0.910, 0.994] | faster | 119.1 / 19.53 / -; 1.925 MB | 119.1 / 19.53 / -; 1.925 MB |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 15.69 ms | 14.97 ms | 0.954 [0.900, 0.978] | faster | 1172 / 156.2 / -; 18.82 MB | 1172 / 156.2 / -; 18.82 MB |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.414 ms | 1.377 ms | 0.974 [0.944, 1.001] | no-change | 105.5 / 15.62 / -; 1.702 MB | 105.5 / 15.62 / -; 1.702 MB |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 14.39 ms | 13.87 ms | 0.964 [0.950, 0.987] | faster | 1078 / 156.2 / -; 17.22 MB | 1078 / 156.2 / -; 17.22 MB |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 5K) | 1.791 ms | 1.762 ms | 0.984 [0.961, 1.028] | no-change | 375 / 74.22 / -; 6.037 MB | 375 / 74.22 / -; 6.037 MB |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 20K) | 7.496 ms | 7.423 ms | 0.990 [0.963, 1.020] | no-change | 1500 / 195.3 / 7.812; 24.00 MB | 1500 / 195.3 / 7.812; 24.00 MB |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 1K) | 6.935 ms | 6.797 ms | 0.980 [0.973, 1.017] | no-change | 234.4 / 39.06 / -; 3.786 MB | 234.4 / 39.06 / -; 3.786 MB |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 69.45 ms | 67.98 ms | 0.979 [0.974, 1.016] | no-change | 2250 / 125 / -; 37.43 MB | 2250 / 125 / -; 37.43 MB |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.230 ms | 1.190 ms | 0.968 [0.906, 1.041] | no-change | 246.1 / 39.06 / -; 3.939 MB | 246.1 / 39.06 / -; 3.939 MB |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 12.48 ms | 12.26 ms | 0.983 [0.935, 1.076] | no-change | 2438 / 156.2 / -; 38.96 MB | 2438 / 156.2 / -; 38.96 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 3.872 ms | 3.878 ms | 1.002 [0.986, 1.022] | no-change | 296.9 / 289.1 / 148.4; 3.787 MB | 296.9 / 289.1 / 148.4; 3.787 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 48.01 ms | 50.73 ms | 1.056 [1.021, 1.080] | slower | 3188 / 3125 / 1000; 41.82 MB | 3188 / 3125 / 1000; 41.82 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 10K) | 1.779 ms | 1.743 ms | 0.980 [0.944, 1.044] | no-change | 250 / 236.3 / -; 4.015 MB | 250 / 236.3 / -; 4.015 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 100K) | 52.66 ms | 52.35 ms | 0.994 [0.973, 1.023] | no-change | 3438 / 3375 / 1125; 39.73 MB | 3438 / 3375 / 1125; 39.73 MB |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 7.040 ms | 6.599 ms | 0.937 [0.925, 0.954] | faster | 328.1 / 328.1 / 328.1; 5.445 MB | 328.1 / 328.1 / 328.1; 5.445 MB |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 70.29 ms | 64.58 ms | 0.919 [0.900, 0.937] | faster | 142.9 / 142.9 / 142.9; 55.37 MB | 250 / 250 / 250; 55.37 MB |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 860.6 ms | 821.1 ms | 0.954 [0.876, 1.008] | no-change | - / - / -; 867.3 MB | - / - / -; 867.3 MB |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 10K) | 51.60 ms | 16.21 ms | 0.314 [0.289, 3.536] | no-change | - / - / -; 84.53 KB | - / - / -; 84.53 KB |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 100K) | 479.9 ms | 465.4 ms | 0.970 [0.250, 3.958] | no-change | - / - / -; 87.23 KB | - / - / -; 87.06 KB |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 1M) | 4.630 s | 4.331 s | 0.936 [0.565, 1.610] | no-change | - / - / -; 89.95 KB | - / - / -; 89.61 KB |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.754 ms | 1.757 ms | 1.001 [0.908, 1.033] | no-change | 248 / 248 / 248; 1.159 MB | 248 / 248 / 248; 1.159 MB |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.02 ms | 16.57 ms | 0.974 [0.929, 1.026] | no-change | 281.2 / 281.2 / 281.2; 11.77 MB | 281.2 / 281.2 / 281.2; 11.77 MB |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 10K) | 1.176 ms | 1.122 ms | 0.954 [0.892, 1.044] | no-change | - / - / -; 72 B | - / - / -; 40 B |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 100K) | 11.94 ms | 11.31 ms | 0.948 [0.891, 1.044] | no-change | - / - / -; 150 B | - / - / -; 0 B |
| FixedWidthWriteBenchmarks.WriteString (Rows 10K) | 1.110 ms | 1.862 ms | 1.678 [1.412, 1.876] | slower | 201.2 / 201.2 / 201.2; 1.567 MB | 234.4 / 234.4 / 234.4; 1.566 MB |
| FixedWidthWriteBenchmarks.WriteString (Rows 100K) | 11.24 ms | 13.95 ms | 1.241 [1.164, 1.284] | slower | 187.5 / 187.5 / 187.5; 15.64 MB | 218.8 / 218.8 / 218.8; 15.64 MB |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 10K) | 947.0 μs | 947.7 μs | 1.001 [0.984, 1.030] | no-change | - / - / -; 40 B | - / - / -; 72 B |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 100K) | 9.523 ms | 9.512 ms | 0.999 [0.982, 1.008] | no-change | - / - / -; 118 B | - / - / -; 145 B |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.343 ms | 8.202 ms | 0.878 [0.836, 0.968] | faster | 453.1 / - / -; 7.397 MB | 453.1 / - / -; 7.397 MB |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 94.05 ms | 82.74 ms | 0.880 [0.822, 0.961] | faster | 4667 / - / -; 75.32 MB | 4714 / - / -; 75.32 MB |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 7.720 ms | 7.548 ms | 0.978 [0.961, 0.987] | no-change | 453.1 / 78.12 / -; 7.422 MB | 460.9 / 85.94 / -; 7.431 MB |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 77.13 ms | 77.78 ms | 1.008 [0.992, 1.035] | no-change | 4571 / 142.9 / -; 75.15 MB | 4714 / 142.9 / -; 75.24 MB |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 3.119 ms | 2.902 ms | 0.930 [0.853, 1.072] | no-change | 93.75 / - / -; 1.609 MB | 93.75 / - / -; 1.609 MB |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 30.45 ms | 28.76 ms | 0.944 [0.801, 1.075] | no-change | 1031 / - / -; 16.76 MB | 1031 / - / -; 16.76 MB |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 7.784 ms | 7.387 ms | 0.949 [0.906, 0.996] | faster | 453.1 / 78.12 / -; 7.422 MB | 453.1 / 78.12 / -; 7.431 MB |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 76.92 ms | 75.38 ms | 0.980 [0.911, 1.044] | no-change | 4571 / 142.9 / -; 75.15 MB | 4714 / 142.9 / -; 75.24 MB |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 10.93 ms | 8.456 ms | 0.774 [0.704, 0.910] | faster | 468.8 / 93.75 / -; 7.549 MB | 468.8 / 93.75 / -; 7.549 MB |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 112.0 ms | 86.98 ms | 0.777 [0.712, 0.896] | faster | 4800 / 1400 / -; 76.84 MB | 4667 / 1167 / -; 76.84 MB |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.206 ms | 5.192 ms | 0.997 [0.981, 1.035] | no-change | 460.9 / 85.94 / -; 7.408 MB | 460.9 / 85.94 / -; 7.408 MB |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 52.12 ms | 51.14 ms | 0.981 [0.942, 0.993] | no-change | 4600 / 200 / -; 74.91 MB | 4600 / 200 / -; 74.91 MB |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 524.6 ms | 515.2 ms | 0.982 [0.960, 1.002] | no-change | 4.7e+04 / - / -; 763.6 MB | 4.7e+04 / - / -; 763.6 MB |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 10K) | 7.837 ms | 7.160 ms | 0.914 [0.787, 0.944] | faster | 453.1 / - / -; 7.350 MB | 453.1 / 7.812 / -; 7.350 MB |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 100K) | 77.42 ms | 72.80 ms | 0.940 [0.901, 0.969] | faster | 4571 / - / -; 74.85 MB | 4571 / - / -; 74.85 MB |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 1M) | 781.6 ms | 722.0 ms | 0.924 [0.901, 0.948] | faster | 4.7e+04 / - / -; 763.6 MB | 4.7e+04 / - / -; 763.6 MB |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.168 ms | 2.134 ms | 0.984 [0.920, 1.014] | no-change | 496.1 / 496.1 / 496.1; 4.950 MB | 496.1 / 496.1 / 496.1; 4.949 MB |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 21.64 ms | 21.70 ms | 1.003 [0.960, 1.075] | no-change | 3062 / 2969 / 1000; 49.30 MB | 3062 / 2969 / 1000; 49.30 MB |

### `fh-intel`

| Scenario | 4.9.1 on .NET 10 | 4.9.1 on .NET 11 | 11/10 [99.9 % CI] | verdict | Gen0 / Gen1 / Gen2; allocated, 4.9.1 on .NET 10 | Gen0 / Gen1 / Gen2; allocated, 4.9.1 on .NET 11 |
|---|---|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 7.604 ms | 7.506 ms | 0.987 [0.970, 0.997] | no-change | 296.9 / 46.88 / -; 7.396 MB | 304.7 / 54.69 / -; 7.396 MB |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 77.46 ms | 76.29 ms | 0.985 [0.971, 0.995] | no-change | 3000 / 142.9 / -; 74.89 MB | 3000 / 142.9 / -; 74.89 MB |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 775.6 ms | 763.2 ms | 0.984 [0.972, 0.996] | no-change | 3.1e+04 / - / -; 763.6 MB | 3.1e+04 / - / -; 763.6 MB |
| OptimizedParseBenchmarks.ReadFile (Rows 10K) | 8.759 ms | 8.671 ms | 0.990 [0.965, 1.001] | no-change | 296.9 / 250 / -; 7.473 MB | 296.9 / 250 / -; 7.472 MB |
| OptimizedParseBenchmarks.ReadFile (Rows 100K) | 149.8 ms | 149.5 ms | 0.998 [0.978, 1.008] | no-change | 4000 / 3750 / 1000; 75.66 MB | 4000 / 3750 / 1000; 75.66 MB |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.826 ms | 2.775 ms | 0.982 [0.951, 1.064] | no-change | 93.75 / 11.72 / -; 2.246 MB | 93.75 / 11.72 / -; 2.246 MB |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 28.65 ms | 28.17 ms | 0.983 [0.952, 1.066] | no-change | 906.2 / 125 / -; 22.03 MB | 906.2 / 125 / -; 22.03 MB |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 10K) | 3.040 ms | 2.914 ms | 0.959 [0.931, 0.975] | faster | 93.75 / 78.12 / -; 2.322 MB | 93.75 / 78.12 / -; 2.322 MB |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 100K) | 51.38 ms | 48.58 ms | 0.946 [0.942, 0.993] | faster | 1250 / 1188 / 375; 22.80 MB | 1250 / 1188 / 375; 22.80 MB |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.619 ms | 2.542 ms | 0.971 [0.950, 0.994] | no-change | 93.75 / 11.72 / -; 2.245 MB | 93.75 / 11.72 / -; 2.245 MB |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.37 ms | 25.81 ms | 0.979 [0.951, 1.000] | no-change | 906.2 / 125 / -; 22.02 MB | 906.2 / 125 / -; 22.02 MB |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.637 ms | 1.585 ms | 0.968 [0.952, 0.977] | faster | 80.08 / 11.72 / -; 1.925 MB | 80.08 / 11.72 / -; 1.925 MB |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 16.52 ms | 16.13 ms | 0.976 [0.956, 0.993] | no-change | 781.2 / 125 / -; 18.82 MB | 781.2 / 125 / -; 18.82 MB |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.467 ms | 1.487 ms | 1.013 [0.980, 1.022] | no-change | 70.31 / 9.766 / -; 1.702 MB | 70.31 / 9.766 / -; 1.702 MB |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 14.89 ms | 14.74 ms | 0.989 [0.963, 1.000] | no-change | 718.8 / 109.4 / -; 17.22 MB | 718.8 / 109.4 / -; 17.22 MB |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 5K) | 1.935 ms | 1.897 ms | 0.980 [0.944, 1.016] | no-change | 250 / 46.88 / -; 6.037 MB | 250 / 42.97 / -; 6.037 MB |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 20K) | 8.140 ms | 8.090 ms | 0.994 [0.961, 1.029] | no-change | 1000 / 187.5 / -; 24.00 MB | 1000 / 187.5 / -; 24.00 MB |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 1K) | 6.435 ms | 6.372 ms | 0.990 [0.979, 1.006] | no-change | 156.2 / 23.44 / -; 3.786 MB | 156.2 / 23.44 / -; 3.786 MB |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 64.70 ms | 64.03 ms | 0.990 [0.978, 1.004] | no-change | 1500 / 125 / -; 37.43 MB | 1500 / 125 / -; 37.43 MB |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.350 ms | 1.373 ms | 1.017 [0.981, 1.060] | no-change | 164.1 / 25.39 / -; 3.939 MB | 164.1 / 25.39 / -; 3.939 MB |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 13.87 ms | 13.79 ms | 0.994 [0.965, 1.032] | no-change | 1625 / 156.2 / -; 38.96 MB | 1625 / 156.2 / -; 38.96 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 2.649 ms | 2.536 ms | 0.957 [0.917, 1.021] | no-change | 164.1 / 164.1 / 164.1; 3.787 MB | 164.1 / 164.1 / 164.1; 3.787 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 50.81 ms | 50.93 ms | 1.002 [0.973, 1.027] | no-change | 2438 / 2375 / 937.5; 41.82 MB | 2438 / 2375 / 937.5; 41.82 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 10K) | 1.910 ms | 1.818 ms | 0.952 [0.895, 0.988] | faster | 166 / 154.3 / -; 4.015 MB | 166 / 156.2 / -; 4.015 MB |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 100K) | 54.37 ms | 54.00 ms | 0.993 [0.982, 1.000] | no-change | 2312 / 2250 / 750; 39.73 MB | 2312 / 2250 / 750; 39.72 MB |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 7.343 ms | 7.093 ms | 0.966 [0.948, 1.007] | no-change | 328.1 / 328.1 / 328.1; 5.445 MB | 328.1 / 328.1 / 328.1; 5.445 MB |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 75.82 ms | 73.73 ms | 0.972 [0.956, 0.983] | no-change | 142.9 / 142.9 / 142.9; 55.37 MB | 142.9 / 142.9 / 142.9; 55.37 MB |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 867.9 ms | 847.0 ms | 0.976 [0.934, 1.007] | no-change | - / - / -; 867.3 MB | - / - / -; 867.3 MB |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 10K) | 16.37 ms | 41.96 ms | 2.564 [0.306, 3.571] | no-change | - / - / -; 84.53 KB | - / - / -; 84.52 KB |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 100K) | 280.1 ms | 171.1 ms | 0.611 [0.240, 3.447] | no-change | - / - / -; 85.88 KB | - / - / -; 85.79 KB |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 1M) | 2.201 s | 2.596 s | 1.179 [0.451, 2.201] | no-change | - / - / -; 89.95 KB | - / - / -; 89.61 KB |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.731 ms | 1.724 ms | 0.996 [0.925, 1.166] | no-change | 248 / 248 / 248; 1.159 MB | 248 / 248 / 248; 1.159 MB |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 16.63 ms | 17.44 ms | 1.048 [0.943, 1.101] | no-change | 281.2 / 281.2 / 281.2; 11.77 MB | 281.2 / 281.2 / 281.2; 11.77 MB |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 10K) | 1.089 ms | 1.038 ms | 0.953 [0.917, 0.964] | faster | - / - / -; 72 B | - / - / -; 40 B |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 100K) | 11.42 ms | 10.78 ms | 0.944 [0.916, 0.973] | faster | - / - / -; 150 B | - / - / -; 0 B |
| FixedWidthWriteBenchmarks.WriteString (Rows 10K) | 1.139 ms | 1.423 ms | 1.249 [0.755, 1.819] | no-change | 200.2 / 200.2 / 200.2; 1.567 MB | 218.8 / 218.8 / 218.8; 1.566 MB |
| FixedWidthWriteBenchmarks.WriteString (Rows 100K) | 12.31 ms | 14.38 ms | 1.168 [1.135, 1.192] | slower | 187.5 / 187.5 / 187.5; 15.64 MB | 187.5 / 187.5 / 187.5; 15.64 MB |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 10K) | 890.5 μs | 785.6 μs | 0.882 [0.878, 0.931] | faster | - / - / -; 40 B | - / - / -; 72 B |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 100K) | 9.253 ms | 8.501 ms | 0.919 [0.909, 0.965] | faster | - / - / -; 118 B | - / - / -; 145 B |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.225 ms | 9.038 ms | 0.980 [0.921, 1.018] | no-change | 296.9 / - / -; 7.397 MB | 273.4 / - / -; 7.397 MB |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 92.74 ms | 92.61 ms | 0.999 [0.930, 1.031] | no-change | 3000 / - / -; 75.32 MB | 3000 / - / -; 75.32 MB |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 8.541 ms | 8.448 ms | 0.989 [0.964, 1.020] | no-change | 296.9 / 46.88 / -; 7.422 MB | 250 / - / -; 7.431 MB |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 85.67 ms | 83.37 ms | 0.973 [0.962, 0.995] | no-change | 3000 / 166.7 / -; 75.15 MB | 3000 / 166.7 / -; 75.24 MB |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 3.163 ms | 3.029 ms | 0.958 [0.914, 0.994] | faster | 62.5 / - / -; 1.609 MB | 66.41 / - / -; 1.609 MB |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 31.56 ms | 29.82 ms | 0.945 [0.895, 1.006] | no-change | 687.5 / - / -; 16.76 MB | 687.5 / - / -; 16.76 MB |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 8.595 ms | 8.475 ms | 0.986 [0.968, 1.041] | no-change | 296.9 / 46.88 / -; 7.422 MB | 250 / - / -; 7.431 MB |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 84.96 ms | 84.04 ms | 0.989 [0.941, 1.051] | no-change | 3000 / 166.7 / -; 75.15 MB | 3000 / 166.7 / -; 75.24 MB |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 9.647 ms | 9.523 ms | 0.987 [0.939, 1.017] | no-change | 312.5 / 46.88 / -; 7.549 MB | 312.5 / 46.88 / -; 7.549 MB |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 97.36 ms | 97.23 ms | 0.999 [0.979, 1.045] | no-change | 3167 / 166.7 / -; 76.84 MB | 3167 / 166.7 / -; 76.83 MB |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.488 ms | 5.304 ms | 0.966 [0.946, 0.989] | faster | 304.7 / 54.69 / -; 7.408 MB | 304.7 / 54.69 / -; 7.408 MB |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 56.96 ms | 54.74 ms | 0.961 [0.942, 1.010] | no-change | 3111 / 111.1 / -; 74.91 MB | 3100 / 200 / -; 74.91 MB |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 569.9 ms | 548.0 ms | 0.962 [0.943, 0.998] | faster | 3.1e+04 / - / -; 763.6 MB | 3.1e+04 / - / -; 763.6 MB |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 10K) | 7.448 ms | 7.031 ms | 0.944 [0.925, 0.958] | faster | 304.7 / - / -; 7.350 MB | 304.7 / - / -; 7.350 MB |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 100K) | 75.39 ms | 71.87 ms | 0.953 [0.943, 0.969] | faster | 3000 / - / -; 74.85 MB | 3000 / - / -; 74.85 MB |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 1M) | 755.9 ms | 718.5 ms | 0.951 [0.941, 0.971] | faster | 3.1e+04 / - / -; 763.6 MB | 3.1e+04 / - / -; 763.6 MB |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.137 ms | 2.125 ms | 0.995 [0.976, 1.026] | no-change | 496.1 / 496.1 / 496.1; 4.950 MB | 496.1 / 496.1 / 496.1; 4.949 MB |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 30.56 ms | 28.79 ms | 0.942 [0.918, 0.971] | faster | 2250 / 2219 / 906.2; 49.30 MB | 2250 / 2219 / 906.2; 49.30 MB |

## Compiler and SDK effect

The published 4.9.1 package (built with SDK 10) against the same source built with SDK 11 (pairs `sdk-net10`,
`sdk-net11`), and runtime async on the 4.9.1 source (`ra-control`). One table per pair and VM.

### `fh-amd`

pkg-net10 -> control-net10:

| Scenario | 4.9.1 pkg | control (SDK 11) | control/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 6.548 ms | 6.536 ms | 0.998 [0.975, 1.037] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 67.13 ms | 66.14 ms | 0.985 [0.967, 1.047] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 669.4 ms | 665.3 ms | 0.994 [0.979, 1.024] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.590 ms | 2.583 ms | 0.997 [0.973, 1.029] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.22 ms | 25.95 ms | 0.989 [0.969, 1.032] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.193 ms | 1.184 ms | 0.992 [0.912, 1.055] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 12.29 ms | 12.06 ms | 0.981 [0.950, 1.021] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 3.799 ms | 3.818 ms | 1.005 [0.983, 1.048] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 47.88 ms | 47.73 ms | 0.997 [0.989, 1.001] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 6.980 ms | 6.961 ms | 0.997 [0.982, 1.054] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 69.44 ms | 69.51 ms | 1.001 [0.978, 1.037] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 850.2 ms | 861.5 ms | 1.013 [0.923, 1.051] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.757 ms | 1.728 ms | 0.983 [0.918, 1.025] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.49 ms | 16.86 ms | 0.964 [0.929, 1.034] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.221 ms | 9.163 ms | 0.994 [0.956, 1.034] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 92.81 ms | 93.42 ms | 1.007 [0.964, 1.054] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.140 ms | 5.133 ms | 0.999 [0.971, 1.015] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 51.96 ms | 51.76 ms | 0.996 [0.985, 1.017] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 520.5 ms | 518.8 ms | 0.997 [0.980, 1.018] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.149 ms | 2.141 ms | 0.996 [0.967, 1.019] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 21.91 ms | 21.72 ms | 0.992 [0.944, 1.031] | no-change |

pkg-net11 -> control-net11-ra-on:

| Scenario | 4.9.1 pkg | control net11.0, ra on | control/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 6.408 ms | 6.351 ms | 0.991 [0.954, 1.045] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 65.86 ms | 66.33 ms | 1.007 [0.963, 1.039] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 651.6 ms | 649.2 ms | 0.996 [0.954, 1.041] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.519 ms | 2.543 ms | 1.009 [0.962, 1.035] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 25.33 ms | 25.41 ms | 1.003 [0.971, 1.038] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.177 ms | 1.168 ms | 0.992 [0.964, 1.016] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 11.88 ms | 11.77 ms | 0.991 [0.904, 1.045] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 3.862 ms | 3.855 ms | 0.998 [0.974, 1.028] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 49.85 ms | 50.06 ms | 1.004 [0.985, 1.023] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 6.554 ms | 6.491 ms | 0.990 [0.973, 1.009] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 64.24 ms | 63.87 ms | 0.994 [0.979, 1.015] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 808.2 ms | 809.2 ms | 1.001 [0.943, 1.080] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.668 ms | 1.647 ms | 0.987 [0.895, 1.040] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 16.63 ms | 16.20 ms | 0.974 [0.915, 1.014] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 7.957 ms | 7.829 ms | 0.984 [0.877, 1.129] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 80.34 ms | 77.38 ms | 0.963 [0.846, 1.133] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.119 ms | 5.163 ms | 1.009 [0.968, 1.105] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 50.93 ms | 51.20 ms | 1.005 [0.961, 1.076] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 507.7 ms | 511.1 ms | 1.007 [0.947, 1.084] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.093 ms | 2.088 ms | 0.997 [0.967, 1.014] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 21.46 ms | 21.85 ms | 1.018 [0.956, 1.074] | no-change |

control-net11-ra-off -> control-net11-ra-on:

| Scenario | control ra off | control ra on | on/off [99.9 % CI] | verdict |
|---|---|---|---|---|
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 7.898 ms | 7.856 ms | 0.995 [0.956, 1.141] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 79.02 ms | 79.31 ms | 1.004 [0.897, 1.281] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 7.344 ms | 7.171 ms | 0.976 [0.921, 1.018] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 76.62 ms | 71.87 ms | 0.938 [0.908, 0.991] | faster |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 2.906 ms | 3.196 ms | 1.100 [0.914, 1.163] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 28.40 ms | 30.39 ms | 1.070 [0.915, 1.182] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 7.320 ms | 6.956 ms | 0.950 [0.908, 1.008] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 75.50 ms | 72.34 ms | 0.958 [0.902, 1.063] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 8.510 ms | 8.225 ms | 0.967 [0.850, 1.025] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 84.23 ms | 84.66 ms | 1.005 [0.870, 1.032] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.111 ms | 5.096 ms | 0.997 [0.964, 1.008] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 50.53 ms | 50.24 ms | 0.994 [0.979, 1.024] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 503.1 ms | 503.5 ms | 1.001 [0.963, 1.046] | no-change |

### `fh-intel`

pkg-net10 -> control-net10:

| Scenario | 4.9.1 pkg | control (SDK 11) | control/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 7.489 ms | 7.491 ms | 1.000 [0.984, 1.016] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 76.88 ms | 76.68 ms | 0.997 [0.982, 1.008] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 772.3 ms | 768.0 ms | 0.994 [0.982, 1.005] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.839 ms | 2.806 ms | 0.988 [0.969, 1.028] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 28.72 ms | 28.49 ms | 0.992 [0.968, 1.023] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.353 ms | 1.336 ms | 0.987 [0.957, 1.040] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 13.72 ms | 13.87 ms | 1.011 [0.963, 1.021] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 2.522 ms | 2.608 ms | 1.034 [0.931, 1.081] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 50.45 ms | 50.28 ms | 0.997 [0.973, 1.022] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 7.294 ms | 7.405 ms | 1.015 [0.972, 1.050] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 76.03 ms | 76.64 ms | 1.008 [0.996, 1.039] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 863.2 ms | 880.1 ms | 1.020 [0.968, 1.048] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.721 ms | 1.697 ms | 0.986 [0.950, 1.035] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.67 ms | 17.17 ms | 0.971 [0.933, 1.048] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.271 ms | 9.101 ms | 0.982 [0.953, 1.085] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 91.74 ms | 91.77 ms | 1.000 [0.983, 1.052] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.446 ms | 5.469 ms | 1.004 [0.958, 1.045] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 56.28 ms | 56.37 ms | 1.002 [0.960, 1.045] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 563.6 ms | 566.2 ms | 1.005 [0.956, 1.038] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.131 ms | 2.147 ms | 1.008 [0.986, 1.039] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 30.54 ms | 30.76 ms | 1.007 [0.966, 1.024] | no-change |

pkg-net11 -> control-net11-ra-on:

| Scenario | 4.9.1 pkg | control net11.0, ra on | control/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 7.373 ms | 7.363 ms | 0.999 [0.983, 1.023] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 75.48 ms | 75.33 ms | 0.998 [0.985, 1.018] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 754.3 ms | 752.6 ms | 0.998 [0.986, 1.015] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.758 ms | 2.745 ms | 0.995 [0.970, 1.025] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 27.90 ms | 27.83 ms | 0.997 [0.971, 1.025] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.348 ms | 1.344 ms | 0.997 [0.959, 1.028] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 13.55 ms | 13.45 ms | 0.992 [0.963, 1.025] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 2.455 ms | 2.516 ms | 1.025 [0.979, 1.055] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 50.15 ms | 50.39 ms | 1.005 [0.976, 1.032] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 6.924 ms | 6.896 ms | 0.996 [0.973, 1.023] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 73.52 ms | 73.11 ms | 0.994 [0.986, 1.010] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 831.6 ms | 827.7 ms | 0.995 [0.947, 1.043] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.716 ms | 1.680 ms | 0.979 [0.892, 1.091] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.24 ms | 17.19 ms | 0.997 [0.925, 1.027] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 8.668 ms | 8.633 ms | 0.996 [0.938, 1.066] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 91.21 ms | 91.77 ms | 1.006 [0.940, 1.064] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.296 ms | 5.304 ms | 1.001 [0.969, 1.011] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 54.25 ms | 54.36 ms | 1.002 [0.967, 1.018] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 546.8 ms | 550.2 ms | 1.006 [0.966, 1.015] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.133 ms | 2.129 ms | 0.998 [0.947, 1.061] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 28.99 ms | 29.09 ms | 1.004 [0.975, 1.083] | no-change |

control-net11-ra-off -> control-net11-ra-on:

| Scenario | control ra off | control ra on | on/off [99.9 % CI] | verdict |
|---|---|---|---|---|
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 8.753 ms | 8.553 ms | 0.977 [0.941, 1.015] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 87.73 ms | 87.04 ms | 0.992 [0.910, 1.059] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 8.374 ms | 8.336 ms | 0.995 [0.939, 1.031] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 82.79 ms | 81.37 ms | 0.983 [0.974, 1.005] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 2.912 ms | 2.878 ms | 0.988 [0.945, 1.056] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 29.02 ms | 28.86 ms | 0.995 [0.970, 1.030] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 8.214 ms | 8.347 ms | 1.016 [0.967, 1.071] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 82.37 ms | 82.62 ms | 1.003 [0.985, 1.020] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 9.207 ms | 9.057 ms | 0.984 [0.939, 1.085] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 92.39 ms | 97.17 ms | 1.052 [0.929, 1.082] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.286 ms | 5.267 ms | 0.996 [0.973, 1.022] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 54.72 ms | 54.26 ms | 0.992 [0.963, 1.019] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 549.0 ms | 547.0 ms | 0.996 [0.961, 1.028] | no-change |

## Scenarios

One table per scenario group and VM. Rows key on the benchmark method and its parameters.

### 25-column read

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 6.534 ms / 6.444 ms | 6.439 ms | 6.338 ms | 6.293 ms | 1.007 [0.978, 1.061] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 66.76 ms / 65.86 ms | 65.80 ms | 64.66 ms | 64.14 ms | 1.008 [0.983, 1.063] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 667.6 ms / 653.3 ms | 662.2 ms | 648.5 ms | 640.0 ms | 1.013 [0.977, 1.056] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 10K) | 7.775 ms / 7.641 ms | 7.701 ms | 7.525 ms | 7.524 ms | 1.000 [0.977, 1.017] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 100K) | 159.0 ms / 160.5 ms | 154.6 ms | 154.4 ms | 155.1 ms | 0.995 [0.977, 1.010] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 7.575 ms / 7.444 ms | 7.557 ms | 7.369 ms | 7.375 ms | 0.999 [0.965, 1.019] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 77.29 ms / 75.91 ms | 76.98 ms | 75.39 ms | 75.71 ms | 0.996 [0.964, 1.012] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 774.3 ms / 760.2 ms | 771.8 ms | 753.6 ms | 758.8 ms | 0.993 [0.957, 1.015] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 10K) | 8.701 ms / 8.671 ms | 8.559 ms | 8.348 ms | 8.401 ms | 0.994 [0.986, 1.015] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 100K) | 149.1 ms / 149.5 ms | 147.0 ms | 144.9 ms | 146.3 ms | 0.990 [0.984, 1.000] | no-change |

### Quoted read

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.419 ms / 1.377 ms | 1.393 ms | 1.353 ms | 1.354 ms | 0.999 [0.986, 1.026] | no-change |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 14.36 ms / 13.87 ms | 14.09 ms | 13.61 ms | 13.57 ms | 1.003 [0.983, 1.035] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.590 ms / 2.528 ms | 2.532 ms | 2.511 ms | 2.550 ms | 0.985 [0.951, 1.028] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.22 ms / 25.54 ms | 25.53 ms | 25.30 ms | 25.71 ms | 0.984 [0.952, 1.068] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 10K) | 2.778 ms / 2.709 ms | 2.766 ms | 2.692 ms | 2.658 ms | 1.013 [0.946, 1.050] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 100K) | 47.34 ms / 47.43 ms | 47.06 ms | 47.57 ms | 47.15 ms | 1.009 [0.975, 1.066] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.453 ms / 2.451 ms | 2.381 ms | 2.377 ms | 2.389 ms | 0.995 [0.971, 1.052] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 24.72 ms / 24.64 ms | 23.89 ms | 23.97 ms | 24.08 ms | 0.995 [0.973, 1.038] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.562 ms / 1.507 ms | 1.520 ms | 1.501 ms | 1.511 ms | 0.993 [0.936, 1.054] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 15.50 ms / 14.97 ms | 15.18 ms | 14.97 ms | 15.10 ms | 0.991 [0.960, 1.026] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.469 ms / 1.487 ms | 1.442 ms | 1.469 ms | 1.463 ms | 1.004 [0.992, 1.049] | no-change |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 14.89 ms / 14.74 ms | 14.68 ms | 14.71 ms | 14.72 ms | 0.999 [0.978, 1.068] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.826 ms / 2.766 ms | 2.794 ms | 2.745 ms | 2.744 ms | 1.000 [0.989, 1.025] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 28.65 ms / 28.02 ms | 28.33 ms | 27.89 ms | 27.88 ms | 1.000 [0.991, 1.028] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 10K) | 3.029 ms / 2.914 ms | 3.029 ms | 2.934 ms | 2.900 ms | 1.012 [0.982, 1.052] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 100K) | 49.31 ms / 48.58 ms | 49.17 ms | 48.70 ms | 48.65 ms | 1.001 [0.989, 1.055] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.587 ms / 2.542 ms | 2.576 ms | 2.554 ms | 2.552 ms | 1.001 [0.975, 1.154] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.19 ms / 25.81 ms | 26.12 ms | 25.96 ms | 25.88 ms | 1.003 [0.978, 1.158] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.639 ms / 1.585 ms | 1.632 ms | 1.567 ms | 1.584 ms | 0.989 [0.979, 1.017] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 16.54 ms / 16.13 ms | 16.52 ms | 15.90 ms | 16.04 ms | 0.991 [0.971, 0.998] | no-change |

### Other plain reads (long lines, 150 columns)

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 5K) | 1.762 ms / 1.762 ms | 1.733 ms | 1.711 ms | 1.714 ms | 0.998 [0.987, 1.020] | no-change |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 20K) | 7.315 ms / 7.423 ms | 7.210 ms | 7.099 ms | 7.071 ms | 1.004 [0.989, 1.024] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 1K) | 6.868 ms / 6.797 ms | 6.662 ms | 6.768 ms | 6.827 ms | 0.991 [0.967, 1.013] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 68.65 ms / 67.98 ms | 66.92 ms | 67.97 ms | 68.01 ms | 0.999 [0.977, 1.021] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 5K) | 1.921 ms / 1.897 ms | 1.891 ms | 1.871 ms | 1.917 ms | 0.976 [0.958, 1.036] | no-change |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 20K) | 8.079 ms / 8.090 ms | 7.924 ms | 7.927 ms | 8.002 ms | 0.991 [0.951, 1.044] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 1K) | 6.444 ms / 6.372 ms | 6.490 ms | 6.313 ms | 6.311 ms | 1.000 [0.985, 1.009] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 64.70 ms / 64.03 ms | 65.22 ms | 63.38 ms | 63.30 ms | 1.001 [0.985, 1.011] | no-change |

### Fixed-width read

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.201 ms / 1.184 ms | 1.181 ms | 1.158 ms | 1.164 ms | 0.995 [0.949, 1.017] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 12.31 ms / 12.06 ms | 12.01 ms | 11.73 ms | 12.01 ms | 0.977 [0.910, 1.065] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 10K) | 1.744 ms / 1.743 ms | 1.701 ms | 1.681 ms | 1.689 ms | 0.996 [0.885, 1.082] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 100K) | 51.13 ms / 52.35 ms | 50.54 ms | 50.15 ms | 50.63 ms | 0.991 [0.959, 1.024] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 3.829 ms / 3.869 ms | 3.821 ms | 3.835 ms | 3.817 ms | 1.005 [0.991, 1.025] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 47.88 ms / 50.29 ms | 47.87 ms | 49.64 ms | 49.47 ms | 1.003 [0.959, 1.010] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.343 ms / 1.357 ms | 1.311 ms | 1.329 ms | 1.344 ms | 0.988 [0.962, 1.034] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 13.72 ms / 13.68 ms | 13.40 ms | 13.35 ms | 13.23 ms | 1.009 [0.985, 1.042] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 10K) | 1.869 ms / 1.818 ms | 1.848 ms | 1.759 ms | 1.757 ms | 1.001 [0.955, 1.040] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 100K) | 54.27 ms / 54.00 ms | 53.88 ms | 53.47 ms | 53.29 ms | 1.003 [0.986, 1.016] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 2.594 ms / 2.497 ms | 2.535 ms | 2.475 ms | 2.496 ms | 0.992 [0.957, 1.017] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 50.45 ms / 50.81 ms | 50.10 ms | 50.04 ms | 50.08 ms | 0.999 [0.977, 1.025] | no-change |

### Writes

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 10K) | 944.7 μs / 947.7 μs | 972.6 μs | 941.3 μs | 943.5 μs | 0.998 [0.990, 1.005] | no-change |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 100K) | 9.497 ms / 9.512 ms | 9.795 ms | 9.485 ms | 9.491 ms | 0.999 [0.985, 1.007] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 10K) | 1.104 ms / 1.862 ms | 1.059 ms | 2.025 ms | 2.031 ms | 0.997 [0.931, 1.049] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 100K) | 11.20 ms / 13.95 ms | 10.94 ms | 14.39 ms | 14.45 ms | 0.996 [0.965, 1.083] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 10K) | 1.167 ms / 1.122 ms | 1.136 ms | 1.132 ms | 1.108 ms | 1.022 [0.872, 1.086] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 100K) | 11.82 ms / 11.31 ms | 11.46 ms | 11.38 ms | 11.19 ms | 1.017 [0.877, 1.094] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.754 ms / 1.697 ms | 1.749 ms | 1.727 ms | 1.673 ms | 1.032 [0.989, 1.104] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.02 ms / 16.61 ms | 17.33 ms | 16.80 ms | 16.46 ms | 1.021 [0.888, 1.046] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 10K) | 34.21 ms / 16.21 ms | 55.40 ms | 53.91 ms | 16.50 ms | 3.268 [0.516, 3.561] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 100K) | 466.9 ms / 465.4 ms | 259.4 ms | 465.3 ms | 327.3 ms | 1.422 [0.260, 3.860] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 1M) | 4.289 s / 4.331 s | 2.386 s | 4.345 s | 2.274 s | 1.910 [0.719, 2.205] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 6.980 ms / 6.584 ms | 6.923 ms | 6.519 ms | 6.483 ms | 1.006 [0.990, 1.024] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 69.64 ms / 64.52 ms | 68.83 ms | 64.21 ms | 63.93 ms | 1.004 [0.980, 1.019] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 858.7 ms / 809.0 ms | 856.1 ms | 810.8 ms | 804.4 ms | 1.008 [0.984, 1.071] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 10K) | 890.2 μs / 785.6 μs | 912.9 μs | 828.4 μs | 829.2 μs | 0.999 [0.981, 1.018] | no-change |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 100K) | 9.232 ms / 8.501 ms | 9.523 ms | 8.833 ms | 8.852 ms | 0.998 [0.985, 1.013] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 10K) | 1.126 ms / 1.423 ms | 1.110 ms | 1.399 ms | 1.370 ms | 1.021 [0.688, 1.447] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 100K) | 12.29 ms / 14.38 ms | 12.33 ms | 14.43 ms | 14.38 ms | 1.003 [0.946, 1.048] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 10K) | 1.096 ms / 1.038 ms | 1.140 ms | 1.091 ms | 1.047 ms | 1.043 [0.933, 1.075] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 100K) | 11.39 ms / 10.78 ms | 11.82 ms | 11.20 ms | 10.75 ms | 1.043 [0.928, 1.077] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.720 ms / 1.724 ms | 1.727 ms | 1.688 ms | 1.745 ms | 0.967 [0.912, 1.027] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.55 ms / 17.28 ms | 17.65 ms | 17.28 ms | 17.26 ms | 1.001 [0.942, 1.045] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 10K) | 16.33 ms / 41.96 ms | 16.26 ms | 16.27 ms | 16.29 ms | 0.999 [0.978, 1.006] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 100K) | 149.4 ms / 171.1 ms | 155.6 ms | 143.0 ms | 143.2 ms | 0.999 [0.943, 1.023] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 1M) | 2.034 s / 2.596 s | 1.931 s | 1.921 s | 1.953 s | 0.984 [0.928, 1.041] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 7.265 ms / 7.019 ms | 7.289 ms | 6.878 ms | 6.909 ms | 0.996 [0.958, 1.036] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 76.04 ms / 73.54 ms | 76.80 ms | 73.36 ms | 73.65 ms | 0.996 [0.984, 1.014] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 867.9 ms / 840.1 ms | 862.6 ms | 840.3 ms | 845.9 ms | 0.993 [0.978, 1.027] | no-change |

### Async

Four methods of `EngineAsyncBenchmarks` use APIs that 4.9.1 does not have (T43, T46): `Engine_WriteStreamAsync_Null`,
`Engine_ReadAsync_Stream`, `Engine_ReadAsync_TextReader` and `Engine_WriteStreamAsync_Null_SourceThrows`. The 4.9.1
builds leave them out (`FILEHELPERS_BASELINE`), so they run only in the pair `ra` and their `4.9.1 pkg` and `net10`
cells stay empty. `Engine_WriteStreamAsync_Null_SourceThrows` is also in the exception-path table.

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 3.002 ms / 2.902 ms | 2.922 ms | 2.950 ms | 2.939 ms | 1.004 [0.872, 1.222] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 30.10 ms / 28.76 ms | 29.42 ms | 29.52 ms | 29.69 ms | 0.995 [0.886, 1.227] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.230 ms / 8.074 ms | 9.078 ms | 7.871 ms | 7.861 ms | 1.001 [0.884, 1.134] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 93.58 ms / 82.06 ms | 92.41 ms | 80.58 ms | 80.32 ms | 1.003 [0.894, 1.076] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Stream (Rows 10K) | - | - | 6.561 ms | 6.558 ms | 1.001 [0.969, 1.043] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Stream (Rows 100K) | - | - | 66.75 ms | 66.47 ms | 1.004 [0.971, 1.027] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_TextReader (Rows 10K) | - | - | 6.478 ms | 6.368 ms | 1.017 [0.993, 1.043] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_TextReader (Rows 100K) | - | - | 65.64 ms | 65.52 ms | 1.002 [0.981, 1.046] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 7.780 ms / 7.548 ms | 7.769 ms | 7.251 ms | 7.486 ms | 0.969 [0.931, 1.005] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 77.26 ms / 77.78 ms | 76.63 ms | 75.17 ms | 77.24 ms | 0.973 [0.926, 1.036] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null (Rows 10K) | - | - | 4.977 ms | 4.996 ms | 0.996 [0.975, 1.021] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null (Rows 100K) | - | - | 52.51 ms | 53.67 ms | 0.978 [0.919, 1.002] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 10K) | - | - | 848.2 μs | 1.054 ms | 0.804 [0.791, 0.816] | faster |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 100K) | - | - | 8.646 ms | 10.76 ms | 0.803 [0.785, 0.813] | faster |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 7.780 ms / 7.387 ms | 7.922 ms | 7.087 ms | 7.670 ms | 0.924 [0.880, 1.011] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 76.87 ms / 75.38 ms | 76.42 ms | 73.23 ms | 77.27 ms | 0.948 [0.906, 1.024] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 3.093 ms / 3.029 ms | 3.027 ms | 2.856 ms | 2.853 ms | 1.001 [0.899, 1.055] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 31.17 ms / 29.82 ms | 30.77 ms | 28.63 ms | 28.63 ms | 1.000 [0.941, 1.061] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.238 ms / 8.882 ms | 9.070 ms | 8.561 ms | 8.588 ms | 0.997 [0.944, 1.061] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 91.74 ms / 92.20 ms | 91.79 ms | 87.10 ms | 86.87 ms | 1.003 [0.943, 1.062] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Stream (Rows 10K) | - | - | 7.473 ms | 7.506 ms | 0.996 [0.989, 1.011] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Stream (Rows 100K) | - | - | 76.12 ms | 76.57 ms | 0.994 [0.987, 1.004] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_TextReader (Rows 10K) | - | - | 7.416 ms | 7.475 ms | 0.992 [0.982, 1.012] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_TextReader (Rows 100K) | - | - | 76.07 ms | 76.64 ms | 0.993 [0.981, 1.008] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 8.497 ms / 8.448 ms | 8.297 ms | 8.256 ms | 8.433 ms | 0.979 [0.961, 1.017] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 85.00 ms / 83.37 ms | 83.31 ms | 81.79 ms | 82.37 ms | 0.993 [0.965, 1.022] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null (Rows 10K) | - | - | 5.090 ms | 5.084 ms | 1.001 [0.987, 1.020] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null (Rows 100K) | - | - | 54.40 ms | 54.41 ms | 1.000 [0.980, 1.017] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 10K) | - | - | 820.5 μs | 1.024 ms | 0.801 [0.766, 0.820] | faster |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 100K) | - | - | 8.450 ms | 10.44 ms | 0.810 [0.778, 0.819] | faster |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 8.540 ms / 8.475 ms | 8.343 ms | 8.190 ms | 8.415 ms | 0.973 [0.957, 1.028] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 84.41 ms / 84.04 ms | 82.92 ms | 81.83 ms | 82.89 ms | 0.987 [0.963, 1.013] | no-change |

### Exception path

A conversion error in every 100th row with `ErrorMode.SaveAndContinue` (`Engine_ReadAsync_Path_ExceptionEvery100`),
and `WriteStreamAsync` from a source that throws after 3 records, `Rows / 100` calls per invocation
(`Engine_WriteStreamAsync_Null_SourceThrows`, 4.10 builds only).

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 10.89 ms / 8.456 ms | 10.61 ms | 8.320 ms | 8.299 ms | 1.003 [0.946, 1.163] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 109.3 ms / 86.98 ms | 108.7 ms | 85.61 ms | 86.24 ms | 0.993 [0.964, 1.100] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 10K) | - | - | 848.2 μs | 1.054 ms | 0.804 [0.791, 0.816] | faster |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 100K) | - | - | 8.646 ms | 10.76 ms | 0.803 [0.785, 0.813] | faster |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 9.584 ms / 9.523 ms | 9.431 ms | 9.369 ms | 9.034 ms | 1.037 [0.953, 1.133] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 97.04 ms / 97.23 ms | 95.84 ms | 97.39 ms | 91.88 ms | 1.060 [0.944, 1.084] | no-change |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 10K) | - | - | 820.5 μs | 1.024 ms | 0.801 [0.766, 0.820] | faster |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 100K) | - | - | 8.450 ms | 10.44 ms | 0.810 [0.778, 0.819] | faster |

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
(BenchmarkDotNet does so between iterations), and its x64 build is published without `IlcInstructionSet`, so its
times are not comparable with the BenchmarkDotNet tables; compare AOT only with AOT.

`fh-amd`:

There is no AOT control build (the 4.9.1 source compiled with SDK 11 under AOT), so each 4.9.1 -> 4.10 ratio below includes the compiler and SDK effect as well as the code change: read it as what a consumer gets. The AOT harness forces a full collection before every timed read and its x64 build does not assume AVX2, so its times are not comparable with the BenchmarkDotNet tables (BENCHMARKS.md, "Native AOT timing harness").

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 93.24 ms / 68.77 ms | 93.11 ms | 67.98 ms | 67.78 ms | 1.007 [0.974, 1.039] | no-change |

aot/pkg-net10 -> aot/net10:

| Scenario | 4.9.1 pkg (AOT) | 4.10 net10 (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 93.24 ms | 93.11 ms | 0.999 [0.993, 1.039] | no-change |

aot/pkg-net11 -> aot/net11-ra-on:

| Scenario | 4.9.1 pkg (AOT) | 4.10 net11-ra-on (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 68.77 ms | 67.80 ms | 0.986 [0.958, 1.013] | no-change |

aot/pkg-net11 -> aot/net11-ra-off:

| Scenario | 4.9.1 pkg (AOT) | 4.10 net11-ra-off (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 68.77 ms | 67.63 ms | 0.983 [0.960, 1.002] | no-change |

`fh-intel`:

There is no AOT control build (the 4.9.1 source compiled with SDK 11 under AOT), so each 4.9.1 -> 4.10 ratio below includes the compiler and SDK effect as well as the code change: read it as what a consumer gets. The AOT harness forces a full collection before every timed read and its x64 build does not assume AVX2, so its times are not comparable with the BenchmarkDotNet tables (BENCHMARKS.md, "Native AOT timing harness").

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 92.99 ms / 75.79 ms | 92.52 ms | 74.55 ms | 74.67 ms | 0.997 [0.988, 1.010] | no-change |

aot/pkg-net10 -> aot/net10:

| Scenario | 4.9.1 pkg (AOT) | 4.10 net10 (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 92.99 ms | 92.52 ms | 0.995 [0.992, 0.999] | no-change |

aot/pkg-net11 -> aot/net11-ra-on:

| Scenario | 4.9.1 pkg (AOT) | 4.10 net11-ra-on (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 75.81 ms | 74.67 ms | 0.985 [0.978, 1.011] | no-change |

aot/pkg-net11 -> aot/net11-ra-off:

| Scenario | 4.9.1 pkg (AOT) | 4.10 net11-ra-off (AOT) | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| Aot.PackageAssets_ParseAndDiscard (Rows 100K) | 75.69 ms | 74.69 ms | 0.987 [0.977, 0.993] | no-change |

The Native AOT smoke test (T04, `tests/ModernLibs.FileHelpers.Aot.Tests`) checks correctness under AOT and measures no
time.

### Controls

Code that is the same in every build (Sylvan, CsvHelper, the span slicer of the fixed-width suite). Their ratios show
the noise of each pair.

`fh-amd`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 10K) | 7.769 ms / 7.160 ms | 7.666 ms | 7.330 ms | 7.218 ms | 1.016 [0.985, 1.156] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 100K) | 77.38 ms / 72.80 ms | 76.64 ms | 74.07 ms | 72.71 ms | 1.019 [0.969, 1.167] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 1M) | 779.7 ms / 722.0 ms | 765.2 ms | 738.7 ms | 727.7 ms | 1.015 [0.968, 1.176] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.164 ms / 5.186 ms | 5.079 ms | 5.238 ms | 5.180 ms | 1.011 [0.936, 1.067] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 51.96 ms / 51.03 ms | 51.52 ms | 50.76 ms | 51.01 ms | 0.995 [0.964, 1.029] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 522.5 ms / 511.2 ms | 517.1 ms | 512.0 ms | 514.8 ms | 0.995 [0.953, 1.052] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.150 ms / 2.097 ms | 2.125 ms | 2.141 ms | 2.135 ms | 1.003 [0.977, 1.031] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 21.63 ms / 21.66 ms | 21.40 ms | 22.98 ms | 23.30 ms | 0.986 [0.932, 1.076] | no-change |

`fh-intel`:

| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |
|---|---|---|---|---|---|---|
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 10K) | 7.427 ms / 7.031 ms | 7.428 ms | 7.030 ms | 7.053 ms | 0.997 [0.972, 1.009] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 100K) | 75.32 ms / 71.87 ms | 74.77 ms | 71.49 ms | 71.83 ms | 0.995 [0.984, 1.008] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 1M) | 755.0 ms / 718.5 ms | 746.4 ms | 715.9 ms | 722.9 ms | 0.990 [0.979, 1.006] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.481 ms / 5.300 ms | 5.417 ms | 5.285 ms | 5.294 ms | 0.998 [0.981, 1.059] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 56.56 ms / 54.57 ms | 55.82 ms | 54.46 ms | 54.44 ms | 1.000 [0.971, 1.027] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 568.2 ms / 547.6 ms | 561.9 ms | 550.9 ms | 549.5 ms | 1.002 [0.967, 1.037] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.137 ms / 2.127 ms | 2.138 ms | 2.068 ms | 2.068 ms | 1.000 [0.961, 1.044] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 30.56 ms / 28.89 ms | 30.52 ms | 28.47 ms | 28.55 ms | 0.997 [0.962, 1.040] | no-change |

### The 4.10 build against the 4.9.1 package

`fh-amd`:

pkg-net10 -> net10:

| Scenario | 4.9.1 pkg | 4.10 net10 | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 6.481 ms | 6.439 ms | 0.994 [0.936, 1.035] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 66.10 ms | 65.80 ms | 0.995 [0.941, 1.057] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 653.8 ms | 662.2 ms | 1.013 [0.954, 1.034] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 10K) | 7.711 ms | 7.701 ms | 0.999 [0.984, 1.017] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 100K) | 156.6 ms | 154.6 ms | 0.988 [0.966, 1.014] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.571 ms | 2.532 ms | 0.985 [0.962, 1.031] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.05 ms | 25.53 ms | 0.980 [0.957, 1.007] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 10K) | 2.761 ms | 2.766 ms | 1.002 [0.962, 1.054] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 100K) | 47.32 ms | 47.06 ms | 0.994 [0.970, 1.011] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.431 ms | 2.381 ms | 0.979 [0.928, 1.019] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 24.41 ms | 23.89 ms | 0.979 [0.919, 1.017] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.559 ms | 1.520 ms | 0.975 [0.939, 1.023] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 15.48 ms | 15.18 ms | 0.981 [0.947, 1.022] | no-change |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.425 ms | 1.393 ms | 0.978 [0.936, 1.020] | no-change |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 14.34 ms | 14.09 ms | 0.982 [0.934, 1.017] | no-change |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 5K) | 1.742 ms | 1.733 ms | 0.995 [0.959, 1.010] | no-change |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 20K) | 7.208 ms | 7.210 ms | 1.000 [0.991, 1.038] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 1K) | 6.814 ms | 6.662 ms | 0.978 [0.951, 1.039] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 67.85 ms | 66.92 ms | 0.986 [0.964, 1.036] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.188 ms | 1.181 ms | 0.994 [0.940, 1.065] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 12.39 ms | 12.01 ms | 0.970 [0.927, 1.017] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 3.930 ms | 3.821 ms | 0.972 [0.960, 1.037] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 47.82 ms | 47.87 ms | 1.001 [0.994, 1.006] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 10K) | 1.733 ms | 1.701 ms | 0.982 [0.944, 1.062] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 100K) | 50.50 ms | 50.54 ms | 1.001 [0.979, 1.028] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 6.965 ms | 6.923 ms | 0.994 [0.990, 1.003] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 69.30 ms | 68.83 ms | 0.993 [0.964, 1.039] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 858.7 ms | 856.1 ms | 0.997 [0.910, 1.068] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 10K) | 16.82 ms | 55.40 ms | 3.294 [0.297, 3.536] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 100K) | 453.8 ms | 259.4 ms | 0.572 [0.261, 3.922] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 1M) | 3.321 s | 2.386 s | 0.719 [0.480, 2.189] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.737 ms | 1.749 ms | 1.007 [0.929, 1.044] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 16.87 ms | 17.33 ms | 1.027 [0.966, 1.055] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 10K) | 1.160 ms | 1.136 ms | 0.979 [0.965, 1.080] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 100K) | 11.77 ms | 11.46 ms | 0.974 [0.962, 1.081] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 10K) | 1.085 ms | 1.059 ms | 0.976 [0.950, 0.984] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 100K) | 11.10 ms | 10.94 ms | 0.986 [0.966, 1.001] | no-change |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 10K) | 942.4 μs | 972.6 μs | 1.032 [1.014, 1.041] | slower |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 100K) | 9.460 ms | 9.795 ms | 1.035 [1.013, 1.041] | slower |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.187 ms | 9.078 ms | 0.988 [0.962, 1.037] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 93.24 ms | 92.41 ms | 0.991 [0.966, 1.053] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 7.845 ms | 7.769 ms | 0.990 [0.898, 1.027] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 77.28 ms | 76.63 ms | 0.992 [0.948, 1.010] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 2.862 ms | 2.922 ms | 1.021 [0.847, 1.077] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 29.35 ms | 29.42 ms | 1.002 [0.848, 1.061] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 7.723 ms | 7.922 ms | 1.026 [0.961, 1.095] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 76.64 ms | 76.42 ms | 0.997 [0.958, 1.070] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 10.81 ms | 10.61 ms | 0.981 [0.935, 1.019] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 108.3 ms | 108.7 ms | 1.004 [0.936, 1.027] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.139 ms | 5.079 ms | 0.988 [0.973, 1.063] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 51.90 ms | 51.52 ms | 0.993 [0.965, 1.057] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 519.7 ms | 517.1 ms | 0.995 [0.972, 1.051] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 10K) | 7.630 ms | 7.666 ms | 1.005 [0.930, 1.041] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 100K) | 76.59 ms | 76.64 ms | 1.001 [0.947, 1.059] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 1M) | 769.9 ms | 765.2 ms | 0.994 [0.949, 1.052] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.143 ms | 2.125 ms | 0.992 [0.983, 1.011] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 21.44 ms | 21.40 ms | 0.998 [0.977, 1.018] | no-change |

`fh-intel`:

pkg-net10 -> net10:

| Scenario | 4.9.1 pkg | 4.10 net10 | 4.10/pkg [99.9 % CI] | verdict |
|---|---|---|---|---|
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 7.593 ms | 7.557 ms | 0.995 [0.975, 1.009] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 77.49 ms | 76.98 ms | 0.993 [0.984, 1.013] | no-change |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 1M) | 775.8 ms | 771.8 ms | 0.995 [0.982, 1.012] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 10K) | 8.630 ms | 8.559 ms | 0.992 [0.970, 1.011] | no-change |
| OptimizedParseBenchmarks.ReadFile (Rows 100K) | 147.8 ms | 147.0 ms | 0.995 [0.985, 1.018] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.814 ms | 2.794 ms | 0.993 [0.967, 1.012] | no-change |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 28.47 ms | 28.33 ms | 0.995 [0.970, 1.016] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 10K) | 3.021 ms | 3.029 ms | 1.003 [0.978, 1.017] | no-change |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers (Rows 100K) | 49.02 ms | 49.17 ms | 1.003 [0.984, 1.039] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 2.581 ms | 2.576 ms | 0.998 [0.968, 1.029] | no-change |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 26.08 ms | 26.12 ms | 1.002 [0.973, 1.031] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.650 ms | 1.632 ms | 0.989 [0.948, 1.017] | no-change |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 16.62 ms | 16.52 ms | 0.994 [0.964, 1.022] | no-change |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 1.470 ms | 1.442 ms | 0.981 [0.960, 1.005] | no-change |
| MultilineBenchmarks.ReadFile_ParseAndDiscard (Rows 100K) | 14.87 ms | 14.68 ms | 0.987 [0.966, 1.014] | no-change |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 5K) | 1.902 ms | 1.891 ms | 0.994 [0.965, 1.069] | no-change |
| LongLineBenchmarks.ReadFile_ParseAndDiscard (Rows 20K) | 7.987 ms | 7.924 ms | 0.992 [0.971, 1.024] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 1K) | 6.452 ms | 6.490 ms | 1.006 [0.998, 1.013] | no-change |
| WideRecordBenchmarks.ReadFile_ParseAndDiscard (Rows 10K) | 64.82 ms | 65.22 ms | 1.006 [0.995, 1.013] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K) | 1.326 ms | 1.311 ms | 0.989 [0.948, 1.047] | no-change |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K) | 13.65 ms | 13.40 ms | 0.982 [0.956, 1.044] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 10K) | 2.510 ms | 2.535 ms | 1.010 [0.944, 1.032] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Struct (Rows 100K) | 50.13 ms | 50.10 ms | 0.999 [0.972, 1.019] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 10K) | 1.861 ms | 1.848 ms | 0.993 [0.966, 1.023] | no-change |
| FixedWidthParseBenchmarks.ReadAllRows_Class (Rows 100K) | 54.03 ms | 53.88 ms | 0.997 [0.979, 1.015] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 10K) | 7.210 ms | 7.289 ms | 1.011 [0.961, 1.032] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 100K) | 76.62 ms | 76.80 ms | 1.002 [0.993, 1.021] | no-change |
| WriteBenchmarks.WriteString_FileHelpers (Rows 1M) | 869.4 ms | 862.6 ms | 0.992 [0.962, 1.028] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 10K) | 16.30 ms | 16.26 ms | 0.997 [0.294, 2.910] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 100K) | 145.8 ms | 155.6 ms | 1.067 [0.938, 4.182] | no-change |
| WriteBenchmarks.WriteFile_FileHelpers (Rows 1M) | 1.978 s | 1.931 s | 0.976 [0.776, 1.490] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 10K) | 1.712 ms | 1.727 ms | 1.009 [0.956, 1.097] | no-change |
| QuotedWriteBenchmarks.WriteString (Rows 100K) | 17.57 ms | 17.65 ms | 1.005 [0.930, 1.035] | no-change |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 10K) | 1.099 ms | 1.140 ms | 1.038 [1.008, 1.057] | slower |
| QuotedWriteBenchmarks.WriteStream_Null (Rows 100K) | 11.37 ms | 11.82 ms | 1.039 [0.998, 1.059] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 10K) | 1.106 ms | 1.110 ms | 1.003 [0.769, 1.113] | no-change |
| FixedWidthWriteBenchmarks.WriteString (Rows 100K) | 12.25 ms | 12.33 ms | 1.006 [0.981, 1.032] | no-change |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 10K) | 890.0 μs | 912.9 μs | 1.026 [1.023, 1.057] | no-change |
| FixedWidthWriteBenchmarks.WriteStream_Null (Rows 100K) | 9.220 ms | 9.523 ms | 1.033 [1.025, 1.057] | slower |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K) | 9.177 ms | 9.070 ms | 0.988 [0.952, 1.029] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 100K) | 91.49 ms | 91.79 ms | 1.003 [0.988, 1.059] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 10K) | 8.477 ms | 8.297 ms | 0.979 [0.958, 1.012] | no-change |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback (Rows 100K) | 84.26 ms | 83.31 ms | 0.989 [0.945, 1.016] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 10K) | 3.058 ms | 3.027 ms | 0.990 [0.965, 1.062] | no-change |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord (Rows 100K) | 30.94 ms | 30.77 ms | 0.994 [0.964, 1.059] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 10K) | 8.432 ms | 8.343 ms | 0.989 [0.935, 1.017] | no-change |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard (Rows 100K) | 83.24 ms | 82.92 ms | 0.996 [0.911, 1.013] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 10K) | 9.459 ms | 9.431 ms | 0.997 [0.965, 1.016] | no-change |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 (Rows 100K) | 96.85 ms | 95.84 ms | 0.990 [0.974, 1.001] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 10K) | 5.511 ms | 5.417 ms | 0.983 [0.961, 1.035] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 100K) | 56.77 ms | 55.82 ms | 0.983 [0.955, 1.038] | no-change |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard (Rows 1M) | 568.8 ms | 561.9 ms | 0.988 [0.956, 1.024] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 10K) | 7.415 ms | 7.428 ms | 1.002 [0.936, 1.021] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 100K) | 75.25 ms | 74.77 ms | 0.994 [0.928, 1.022] | no-change |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard (Rows 1M) | 751.7 ms | 746.4 ms | 0.993 [0.926, 1.014] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 10K) | 2.150 ms | 2.138 ms | 0.994 [0.948, 1.256] | no-change |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer (Rows 100K) | 30.71 ms | 30.52 ms | 0.994 [0.895, 1.118] | no-change |

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
gate is read from that run. `round-report.py` prints a note under the G2 table when this applies. It did not apply in
this round: every gate ratio has the verdict `faster`.

**R6_Current against the 4.9.1 package.** The gate compares two methods of one 4.10 build. "Faster than 4.9.1" holds
only if `R6_Current` measures what 4.9.1 does: the same VM runs `R6_Current` on the 4.9.1 package and on the 4.10 build,
interleaved (pairs `v410-net10`, `v410-net11`), next to the package's `QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard`
(the same work). If `R6_Current` of the 4.10 build differs from the package's by the B4 rule, the gate ratio is not the
ratio against 4.9.1, and the column `R6_PrecomputedEnds 4.10 / R6_Current pkg` gives that ratio directly.

### `fh-amd`

| Build | Rows | R6_Current | R6_PrecomputedEnds | ratio [99.9 % CI] | gate | R6_PrecomputedEnds_FastUnquote | ratio [99.9 % CI] | at 6 % |
|---|---|---|---|---|---|---|---|---|
| net10 | Rows 10K | 2.583 ms | 2.311 ms | 0.895 [0.880, 0.968] | (informative) | 1.961 ms | 0.759 [0.736, 0.783] | (informative) |
| net10 | Rows 100K | 26.02 ms | 23.51 ms | 0.903 [0.876, 0.974] | meets | 19.83 ms | 0.762 [0.742, 0.782] | meets |
| net11-ra-on | Rows 10K | 2.617 ms | 2.189 ms | 0.836 [0.799, 0.901] | (informative) | 1.781 ms | 0.681 [0.652, 0.720] | (informative) |
| net11-ra-on | Rows 100K | 26.23 ms | 22.04 ms | 0.840 [0.809, 0.906] | meets | 17.95 ms | 0.684 [0.666, 0.733] | meets |

- R6_PrecomputedEnds meets the 6 % gate on net10 at 100K rows (ratio 0.903, verdict faster).
- R6_PrecomputedEnds meets the 6 % gate on net11-ra-on at 100K rows (ratio 0.840, verdict faster).

R6_Current against the 4.9.1 package (same VM, interleaved):

| Pair | Rows | R6_Current 4.9.1 pkg | R6_Current 4.10 | 4.10/pkg [99.9 % CI] | verdict | R6_PrecomputedEnds 4.10 / R6_Current pkg [99.9 % CI] | quoted parse-and-discard, 4.9.1 pkg |
|---|---|---|---|---|---|---|---|
| pkg-net10 -> net10 | Rows 10K | 2.621 ms | 2.583 ms | 0.985 [0.938, 1.032] | no-change | 0.882 [0.843, 0.963] faster | 2.590 ms |
| pkg-net10 -> net10 | Rows 100K | 26.32 ms | 26.02 ms | 0.988 [0.947, 1.032] | no-change | 0.893 [0.848, 0.969] faster | 26.22 ms |
| pkg-net11 -> net11-ra-on | Rows 10K | 2.537 ms | 2.617 ms | 1.032 [0.979, 1.058] | no-change | 0.863 [0.819, 0.918] faster | 2.528 ms |
| pkg-net11 -> net11-ra-on | Rows 100K | 25.63 ms | 26.23 ms | 1.023 [0.984, 1.046] | no-change | 0.860 [0.831, 0.913] faster | 25.54 ms |

### `fh-intel`

| Build | Rows | R6_Current | R6_PrecomputedEnds | ratio [99.9 % CI] | gate | R6_PrecomputedEnds_FastUnquote | ratio [99.9 % CI] | at 6 % |
|---|---|---|---|---|---|---|---|---|
| net10 | Rows 10K | 2.796 ms | 2.373 ms | 0.849 [0.824, 0.862] | (informative) | 2.013 ms | 0.720 [0.702, 0.735] | (informative) |
| net10 | Rows 100K | 28.35 ms | 24.04 ms | 0.848 [0.822, 0.861] | meets | 20.34 ms | 0.718 [0.700, 0.734] | meets |
| net11-ra-on | Rows 10K | 2.746 ms | 2.249 ms | 0.819 [0.798, 0.833] | (informative) | 1.850 ms | 0.674 [0.659, 0.686] | (informative) |
| net11-ra-on | Rows 100K | 27.85 ms | 22.71 ms | 0.815 [0.794, 0.827] | meets | 18.85 ms | 0.677 [0.659, 0.691] | meets |

- R6_PrecomputedEnds meets the 6 % gate on net10 at 100K rows (ratio 0.848, verdict faster).
- R6_PrecomputedEnds meets the 6 % gate on net11-ra-on at 100K rows (ratio 0.815, verdict faster).

R6_Current against the 4.9.1 package (same VM, interleaved):

| Pair | Rows | R6_Current 4.9.1 pkg | R6_Current 4.10 | 4.10/pkg [99.9 % CI] | verdict | R6_PrecomputedEnds 4.10 / R6_Current pkg [99.9 % CI] | quoted parse-and-discard, 4.9.1 pkg |
|---|---|---|---|---|---|---|---|
| pkg-net10 -> net10 | Rows 10K | 2.799 ms | 2.796 ms | 0.999 [0.977, 1.028] | no-change | 0.848 [0.822, 0.868] faster | 2.826 ms |
| pkg-net10 -> net10 | Rows 100K | 28.40 ms | 28.35 ms | 0.998 [0.974, 1.026] | no-change | 0.847 [0.820, 0.861] faster | 28.65 ms |
| pkg-net11 -> net11-ra-on | Rows 10K | 2.769 ms | 2.746 ms | 0.992 [0.976, 1.016] | no-change | 0.812 [0.790, 0.826] faster | 2.766 ms |
| pkg-net11 -> net11-ra-on | Rows 100K | 28.04 ms | 27.85 ms | 0.993 [0.978, 1.022] | no-change | 0.810 [0.791, 0.826] faster | 28.02 ms |

Decision: **meets** on both VMs and both runtimes. `R6_PrecomputedEnds / R6_Current` at 100K rows: 0.903 (`fh-amd`,
`net10`), 0.840 (`fh-amd`, `net11-ra-on`), 0.848 (`fh-intel`, `net10`), 0.815 (`fh-intel`, `net11-ra-on`), each with
the verdict `faster`; `R6_PrecomputedEnds_FastUnquote` meets as well (0.677 to 0.762), so the two do not fall on
different sides of 6 %. `R6_Current` of the 4.10 build reads no change against the package's (0.988 to 1.023), so the
gate ratio is the ratio against 4.9.1. T20 is built (design 3.6).

## Gate G2

**Rule (design 3.2).** `G2_SingleSpan` makes `WriteString` of 100K 25-column records at least 10 % faster than
`G2_Current` (ratio at most 0.90 with the B4 verdict `faster`), inside one 4.10 build, on the reference VM and one more.

### `fh-amd`

| Build | Rows | G2_Current | G2_SingleSpan | ratio [99.9 % CI] | gate (at least 10 % faster) |
|---|---|---|---|---|---|
| net10 | Rows 100K | 70.58 ms | 34.33 ms | 0.486 [0.471, 0.500] | meets |
| net11-ra-on | Rows 100K | 64.94 ms | 33.77 ms | 0.520 [0.504, 0.570] | meets |

G2_SingleSpan still copies each record from the engine's record buffer into the result (it does not format straight into the PooledStringWriter chunk): it measures G2 without P5, so a result just under 10 % understates what Phase 3 (G2, G3 and P5) can give.

### `fh-intel`

| Build | Rows | G2_Current | G2_SingleSpan | ratio [99.9 % CI] | gate (at least 10 % faster) |
|---|---|---|---|---|---|
| net10 | Rows 100K | 76.53 ms | 43.04 ms | 0.562 [0.551, 0.579] | meets |
| net11-ra-on | Rows 100K | 75.11 ms | 42.98 ms | 0.572 [0.552, 0.589] | meets |

G2_SingleSpan still copies each record from the engine's record buffer into the result (it does not format straight into the PooledStringWriter chunk): it measures G2 without P5, so a result just under 10 % understates what Phase 3 (G2, G3 and P5) can give.

**The prototype does not include P5.** `round-report.py` prints this note under the G2 table: `G2_SingleSpan` still
copies each record from the engine's record buffer into the result (it does not format straight into the
`PooledStringWriter` chunk), so it measures G2 without P5. A result just under 10 % understates what Phase 3 (G2, G3
and P5 together) can give; the decision should say so. Only the 100K row is a gate row.

Decision: **meets** on both VMs and both runtimes: 0.486 (`fh-amd`, `net10`), 0.520 (`fh-amd`, `net11-ra-on`), 0.562
(`fh-intel`, `net10`), 0.572 (`fh-intel`, `net11-ra-on`), each `faster`. Phase 3 uses the full `RecordWriter` design of
design 5.1 (design 3.6).

## Coding-pattern choices

Design 2.5 items 1, 2 and 3, each per runtime (`net10`, `net11-ra-on`, pair `runtime-410`) and per architecture (one
table per VM). Each row compares two methods of one build, measured in the same run.

1. Safe span vector loop (`Scan_SpanCreate`) against the `Unsafe` loop (`Scan_LoadUnsafe`).
2. Converter call shapes for the write path: `Format_GenericWriter` and `Format_DirectCall` against
   `Format_GenericHelper`.
3. The "any match first" block test (`Tokenizer_SinglePassVector_AnyMatchFirst`) against `Tokenizer_SinglePassVector`;
   expected to cost on Arm64.

### `fh-amd`

| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |
|---|---|---|---|---|---|---|
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset LongLines | net10 | 171.6 μs | 188.3 μs | 1.098 [1.085, 1.307] | slower |
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset LongLines | net11-ra-on | 213.6 μs | 221.4 μs | 1.036 [0.890, 1.288] | no-change |
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset Quoted | net10 | 283.0 μs | 290.9 μs | 1.028 [1.026, 1.032] | no-change |
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset Quoted | net11-ra-on | 286.3 μs | 289.8 μs | 1.012 [1.002, 1.026] | no-change |
| 2: converter call shapes for the write path: Format_GenericWriter against Format_GenericHelper | - | net10 | 56.75 μs | 35.77 μs | 0.630 [0.574, 0.662] | faster |
| 2: converter call shapes for the write path: Format_GenericWriter against Format_GenericHelper | - | net11-ra-on | 54.90 μs | 46.22 μs | 0.842 [0.813, 0.884] | faster |
| 2: converter call shapes for the write path: Format_DirectCall against Format_GenericHelper | - | net10 | 56.75 μs | 25.77 μs | 0.454 [0.412, 0.470] | faster |
| 2: converter call shapes for the write path: Format_DirectCall against Format_GenericHelper | - | net11-ra-on | 54.90 μs | 26.81 μs | 0.488 [0.470, 0.524] | faster |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns150 | net10 | 194.0 μs | 201.5 μs | 1.038 [0.946, 1.152] | no-change |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns150 | net11-ra-on | 196.0 μs | 201.0 μs | 1.026 [0.950, 1.139] | no-change |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns25 | net10 | 582.0 μs | 591.3 μs | 1.016 [0.988, 1.054] | no-change |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns25 | net11-ra-on | 573.7 μs | 592.3 μs | 1.032 [0.993, 1.052] | no-change |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset LongLines | net10 | 147.8 μs | 170.7 μs | 1.155 [0.956, 1.224] | no-change |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset LongLines | net11-ra-on | 147.5 μs | 169.7 μs | 1.150 [0.954, 1.214] | no-change |

### `fh-intel`

| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |
|---|---|---|---|---|---|---|
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset LongLines | net10 | 218.8 μs | 253.0 μs | 1.156 [1.101, 1.161] | slower |
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset LongLines | net11-ra-on | 224.6 μs | 251.8 μs | 1.121 [1.096, 1.168] | slower |
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset Quoted | net10 | 285.6 μs | 301.2 μs | 1.055 [1.033, 1.059] | slower |
| 1: safe span vector loop against the Unsafe loop: Scan_SpanCreate against Scan_LoadUnsafe | Dataset Quoted | net11-ra-on | 332.9 μs | 310.2 μs | 0.932 [0.878, 1.062] | no-change |
| 2: converter call shapes for the write path: Format_GenericWriter against Format_GenericHelper | - | net10 | 50.16 μs | 29.28 μs | 0.584 [0.582, 0.586] | faster |
| 2: converter call shapes for the write path: Format_GenericWriter against Format_GenericHelper | - | net11-ra-on | 47.34 μs | 40.05 μs | 0.846 [0.844, 0.847] | faster |
| 2: converter call shapes for the write path: Format_DirectCall against Format_GenericHelper | - | net10 | 50.16 μs | 22.12 μs | 0.441 [0.439, 0.446] | faster |
| 2: converter call shapes for the write path: Format_DirectCall against Format_GenericHelper | - | net11-ra-on | 47.34 μs | 25.05 μs | 0.529 [0.526, 0.535] | faster |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns150 | net10 | 178.0 μs | 205.2 μs | 1.153 [0.902, 1.226] | no-change |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns150 | net11-ra-on | 180.0 μs | 200.1 μs | 1.111 [1.010, 1.185] | slower |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns25 | net10 | 557.5 μs | 622.4 μs | 1.116 [1.033, 1.135] | slower |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset Columns25 | net11-ra-on | 560.8 μs | 604.0 μs | 1.077 [1.039, 1.127] | slower |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset LongLines | net10 | 199.2 μs | 220.8 μs | 1.108 [1.078, 1.162] | slower |
| 3: "any match first" block test in the tokenizer: Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_SinglePassVector | Dataset LongLines | net11-ra-on | 198.3 μs | 220.3 μs | 1.111 [1.075, 1.157] | slower |

Choice per item:

1. The safe span form is slower on long lines on .NET 10 on both VMs (1.098 `fh-amd`, 1.156 `fh-intel`), on .NET 11 on
   `fh-intel` (1.121) and on the quoted set on `fh-intel` .NET 10 (1.055): the `LoadUnsafe` loads of
   `QuoteBalanceScanner` stay (T33, [v4.10.0.md](v4.10.0.md), "Safe-code audit").
2. `Format_DirectCall` is the fastest shape on both runtimes and VMs (0.454 and 0.441 on .NET 10, 0.488 and 0.529 on
   .NET 11), and the generic writer loses more on .NET 11 (0.842 and 0.846 against 0.630 and 0.584): T22 takes the
   direct-call shape. The T19 and T25 rounds confirm it (v4.10.0.md, gate table, "T22 confirmation").
3. "Any match first" is slower on x64 in most rows on `fh-intel` (1.077 to 1.116) and reads no change on `fh-amd`:
   it stays off on x64; Arm64 was not measured (owner waiver, branch B of T21).

## Component benchmarks

B3: today's code against the candidates of R3, R4 and G2, per runtime: `Tokenizer_*` against
`Tokenizer_CountPlusIndexOf`, `Unquote_IndexOf` against `Unquote_Current` (`Values`: `All`, `DoubledOnly`),
`CreateString_Cached` against `CreateString_New`, `FormatRecord_SingleSpan` against `FormatRecord_Current`.

### `fh-amd`

| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |
|---|---|---|---|---|---|---|
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net10 | 1.315 ms | 540.2 μs | 0.411 [0.386, 0.418] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net11-ra-on | 1.340 ms | 520.8 μs | 0.389 [0.376, 0.410] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net10 | 1.315 ms | 194.0 μs | 0.148 [0.141, 0.161] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net11-ra-on | 1.340 ms | 196.0 μs | 0.146 [0.139, 0.157] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net10 | 1.315 ms | 201.5 μs | 0.153 [0.151, 0.164] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net11-ra-on | 1.340 ms | 201.0 μs | 0.150 [0.148, 0.159] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net10 | 2.085 ms | 1.605 ms | 0.770 [0.762, 0.789] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net11-ra-on | 2.106 ms | 1.617 ms | 0.768 [0.755, 0.790] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net10 | 2.085 ms | 582.0 μs | 0.279 [0.274, 0.283] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net11-ra-on | 2.106 ms | 573.7 μs | 0.272 [0.270, 0.278] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net10 | 2.085 ms | 591.3 μs | 0.284 [0.279, 0.289] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net11-ra-on | 2.106 ms | 592.3 μs | 0.281 [0.275, 0.285] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset LongLines | net10 | 423.1 μs | 1.055 ms | 2.493 [2.462, 2.598] | slower |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset LongLines | net11-ra-on | 423.5 μs | 1.080 ms | 2.551 [2.477, 2.584] | slower |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset LongLines | net10 | 423.1 μs | 147.8 μs | 0.349 [0.341, 0.358] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset LongLines | net11-ra-on | 423.5 μs | 147.5 μs | 0.348 [0.343, 0.353] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset LongLines | net10 | 423.1 μs | 170.7 μs | 0.403 [0.337, 0.423] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset LongLines | net11-ra-on | 423.5 μs | 169.7 μs | 0.401 [0.335, 0.420] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values All | net10 | 585.0 μs | 508.9 μs | 0.870 [0.839, 0.916] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values All | net11-ra-on | 569.3 μs | 499.0 μs | 0.876 [0.842, 0.911] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values DoubledOnly | net10 | 442.2 μs | 397.0 μs | 0.898 [0.884, 1.002] | no-change |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values DoubledOnly | net11-ra-on | 443.6 μs | 404.0 μs | 0.911 [0.845, 0.936] | faster |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Columns25 | net10 | 1.375 ms | 1.373 ms | 0.999 [0.954, 1.029] | no-change |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Columns25 | net11-ra-on | 1.318 ms | 1.366 ms | 1.036 [0.961, 1.074] | no-change |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Quoted | net10 | 206.3 μs | 126.5 μs | 0.613 [0.565, 0.651] | faster |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Quoted | net11-ra-on | 199.2 μs | 124.7 μs | 0.626 [0.602, 0.646] | faster |
| FormatOneRecordBenchmarks.FormatRecord_SingleSpan against FormatRecord_Current | - | net10 | 475.2 μs | 163.1 μs | 0.343 [0.332, 0.350] | faster |
| FormatOneRecordBenchmarks.FormatRecord_SingleSpan against FormatRecord_Current | - | net11-ra-on | 435.6 μs | 160.9 μs | 0.369 [0.360, 0.371] | faster |

### `fh-intel`

| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |
|---|---|---|---|---|---|---|
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net10 | 1.735 ms | 754.2 μs | 0.435 [0.416, 0.453] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net11-ra-on | 1.744 ms | 723.8 μs | 0.415 [0.413, 0.431] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net10 | 1.735 ms | 178.0 μs | 0.103 [0.102, 0.125] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net11-ra-on | 1.744 ms | 180.0 μs | 0.103 [0.100, 0.112] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net10 | 1.735 ms | 205.2 μs | 0.118 [0.113, 0.125] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns150 | net11-ra-on | 1.744 ms | 200.1 μs | 0.115 [0.112, 0.120] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net10 | 2.720 ms | 1.740 ms | 0.640 [0.627, 0.645] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net11-ra-on | 2.724 ms | 1.711 ms | 0.628 [0.626, 0.653] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net10 | 2.720 ms | 557.5 μs | 0.205 [0.201, 0.216] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net11-ra-on | 2.724 ms | 560.8 μs | 0.206 [0.203, 0.212] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net10 | 2.720 ms | 622.4 μs | 0.229 [0.219, 0.231] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset Columns25 | net11-ra-on | 2.724 ms | 604.0 μs | 0.222 [0.219, 0.231] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset LongLines | net10 | 426.5 μs | 1.154 ms | 2.705 [2.686, 2.801] | slower |
| TokenizerBenchmarks.Tokenizer_SinglePassScalar against Tokenizer_CountPlusIndexOf | Dataset LongLines | net11-ra-on | 432.6 μs | 1.179 ms | 2.725 [2.625, 2.761] | slower |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset LongLines | net10 | 426.5 μs | 199.2 μs | 0.467 [0.445, 0.475] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector against Tokenizer_CountPlusIndexOf | Dataset LongLines | net11-ra-on | 432.6 μs | 198.3 μs | 0.458 [0.438, 0.469] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset LongLines | net10 | 426.5 μs | 220.8 μs | 0.518 [0.505, 0.524] | faster |
| TokenizerBenchmarks.Tokenizer_SinglePassVector_AnyMatchFirst against Tokenizer_CountPlusIndexOf | Dataset LongLines | net11-ra-on | 432.6 μs | 220.3 μs | 0.509 [0.496, 0.516] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values All | net10 | 619.4 μs | 542.5 μs | 0.876 [0.817, 0.906] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values All | net11-ra-on | 616.1 μs | 542.6 μs | 0.881 [0.837, 0.912] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values DoubledOnly | net10 | 461.7 μs | 414.7 μs | 0.898 [0.825, 0.938] | faster |
| UnquoteBenchmarks.Unquote_IndexOf against Unquote_Current | Values DoubledOnly | net11-ra-on | 460.1 μs | 413.7 μs | 0.899 [0.876, 0.923] | faster |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Columns25 | net10 | 1.467 ms | 1.423 ms | 0.970 [0.940, 1.043] | no-change |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Columns25 | net11-ra-on | 1.432 ms | 1.401 ms | 0.978 [0.941, 1.070] | no-change |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Quoted | net10 | 199.2 μs | 126.2 μs | 0.634 [0.585, 0.690] | faster |
| CreateStringBenchmarks.CreateString_Cached against CreateString_New | Dataset Quoted | net11-ra-on | 191.0 μs | 124.1 μs | 0.650 [0.622, 0.681] | faster |
| FormatOneRecordBenchmarks.FormatRecord_SingleSpan against FormatRecord_Current | - | net10 | 477.4 μs | 163.4 μs | 0.342 [0.333, 0.352] | faster |
| FormatOneRecordBenchmarks.FormatRecord_SingleSpan against FormatRecord_Current | - | net11-ra-on | 444.7 μs | 158.8 μs | 0.357 [0.351, 0.371] | faster |

## Runtime async

Not a gate (D4). `net11-ra-on` against `net11-ra-off` for every scenario (pair `ra`), and the same on the 4.9.1 source
(`ra-control`). Every case that is slower with runtime async on by the B4 rule is listed here for the owner, with
whether it is slower than the largest control deviation of the same pair ("beyond control noise").

`fh-amd`:

**net11-ra-off -> net11-ra-on**: 65 cases; 2 faster, 63 no-change. Control noise: largest |on/off - 1| of 8 control cases = 1.9 %.

No case is slower with runtime async on by the B4 rule.

**control-net11-ra-off -> control-net11-ra-on**: 13 cases; 1 faster, 12 no-change. Control noise: largest |on/off - 1| of 3 control cases = 0.6 %.

No case is slower with runtime async on by the B4 rule.

`fh-intel`:

**net11-ra-off -> net11-ra-on**: 65 cases; 2 faster, 63 no-change. Control noise: largest |on/off - 1| of 8 control cases = 1.0 %.

No case is slower with runtime async on by the B4 rule.

**control-net11-ra-off -> control-net11-ra-on**: 13 cases; 13 no-change. Control noise: largest |on/off - 1| of 3 control cases = 0.8 %.

No case is slower with runtime async on by the B4 rule.

No case is slower with runtime async on, in either pair on either VM, so nothing is listed for the owner.

## Slope from 10K to 100K rows

Time per row at 10K and at 100K rows, per build, for every scenario measured at both sizes; CsvHelper
(`DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard`) is the reference whose growth is smaller (design 3.1). The last
two columns (Gen0, Gen1, Gen2 per 1,000 operations and bytes allocated per row) compare `net10` with `net11-ra-on` and
the 4.9.1 package (design 3.1 B1); a growth in Gen1 or Gen2 collections from 10K to 100K is the first thing to check
for the slope.

### `fh-amd`

| Benchmark | Build | 10K rows: ns per row | 100K rows: ns per row | 100K/10K | 10K: Gen0 / Gen1 / Gen2; allocated | 100K: Gen0 / Gen1 / Gen2; allocated |
|---|---|---|---|---|---|---|
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord | pkg-net10 | 300.2 | 301.0 | 1.003 | 93.75 / - / -; 169 B per row | 1031 / - / -; 176 B per row |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord | net10 | 292.2 | 294.2 | 1.007 | 93.75 / - / -; 169 B per row | 1031 / - / -; 176 B per row |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord | net11-ra-on | 295.0 | 295.2 | 1.001 | 93.75 / - / -; 169 B per row | 1031 / - / -; 176 B per row |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard | pkg-net10 | 776.9 | 773.8 | 0.996 | 453.1 / - / -; 771 B per row | 4571 / - / -; 785 B per row |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard | net10 | 766.6 | 766.4 | 1.000 | 453.1 / 7.812 / -; 771 B per row | 4571 / - / -; 785 B per row |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard | net11-ra-on | 733.0 | 740.7 | 1.011 | 453.1 / 7.812 / -; 771 B per row | 4571 / - / -; 785 B per row |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 653.4 | 667.6 | 1.022 | 460.9 / 85.94 / -; 776 B per row | 4625 / 125 / -; 785 B per row |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard | net10 | 643.9 | 658.0 | 1.022 | 460.9 / 85.94 / -; 776 B per row | 4625 / 125 / -; 785 B per row |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 633.8 | 646.6 | 1.020 | 460.9 / 85.94 / -; 776 B per row | 4625 / 125 / -; 785 B per row |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard | pkg-net10 | 516.4 | 519.6 | 1.006 | 460.9 / 85.94 / -; 777 B per row | 4600 / 200 / -; 785 B per row |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard | net10 | 507.9 | 515.2 | 1.014 | 460.9 / 85.94 / -; 777 B per row | 4600 / 200 / -; 785 B per row |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard | net11-ra-on | 523.8 | 507.6 | 0.969 | 460.9 / 85.94 / -; 777 B per row | 4600 / 200 / -; 785 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path | pkg-net10 | 923.0 | 935.8 | 1.014 | 453.1 / - / -; 776 B per row | 4667 / - / -; 790 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path | net10 | 907.8 | 924.1 | 1.018 | 453.1 / - / -; 776 B per row | 4667 / - / -; 790 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path | net11-ra-on | 787.1 | 805.8 | 1.024 | 453.1 / - / -; 780 B per row | 4667 / - / -; 794 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 | pkg-net10 | 1088.6 | 1092.7 | 1.004 | 468.8 / 93.75 / -; 792 B per row | 4800 / 1400 / -; 806 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 | net10 | 1060.7 | 1087.2 | 1.025 | 468.8 / 93.75 / -; 792 B per row | 4800 / 1400 / -; 806 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 | net11-ra-on | 832.0 | 856.1 | 1.029 | 468.8 / 93.75 / -; 796 B per row | 4833 / 1167 / -; 810 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Stream | net11-ra-on | 656.1 | 667.5 | 1.017 | 453.1 / - / -; 771 B per row | 4625 / - / -; 785 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_TextReader | net11-ra-on | 647.8 | 656.4 | 1.013 | 453.1 / 7.812 / -; 771 B per row | 4625 / - / -; 785 B per row |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback | pkg-net10 | 778.0 | 772.6 | 0.993 | 453.1 / 78.12 / -; 778 B per row | 4571 / 142.9 / -; 788 B per row |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback | net10 | 776.9 | 766.3 | 0.986 | 453.1 / 78.12 / -; 778 B per row | 4571 / 142.9 / -; 788 B per row |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback | net11-ra-on | 725.1 | 751.7 | 1.037 | 453.1 / 78.12 / -; 778 B per row | 4571 / 142.9 / -; 787 B per row |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null | net11-ra-on | 497.7 | 525.1 | 1.055 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows | net11-ra-on | 84.8 | 86.5 | 1.019 | 9.766 / - / -; 17 B per row | 93.75 / - / -; 17 B per row |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 120.1 | 123.1 | 1.025 | 246.1 / 39.06 / -; 413 B per row | 2438 / 156.2 / -; 409 B per row |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard | net10 | 118.1 | 120.1 | 1.017 | 246.1 / 39.06 / -; 413 B per row | 2438 / 156.2 / -; 409 B per row |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 115.8 | 117.3 | 1.013 | 246.1 / 39.06 / -; 413 B per row | 2438 / 156.2 / -; 409 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Class | pkg-net10 | 174.4 | 511.3 | 2.931 | 250 / 236.3 / -; 421 B per row | 3438 / 3375 / 1125; 417 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Class | net10 | 170.1 | 505.4 | 2.971 | 250 / 236.3 / -; 421 B per row | 3438 / 3375 / 1125; 417 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Class | net11-ra-on | 168.1 | 501.5 | 2.983 | 250 / 236.3 / -; 421 B per row | 3438 / 3375 / 1125; 417 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Struct | pkg-net10 | 382.9 | 478.8 | 1.250 | 296.9 / 289.1 / 148.4; 397 B per row | 3188 / 3125 / 1000; 438 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Struct | net10 | 382.1 | 478.7 | 1.253 | 296.9 / 289.1 / 148.4; 397 B per row | 3188 / 3125 / 1000; 438 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Struct | net11-ra-on | 383.5 | 496.4 | 1.294 | 296.9 / 289.1 / 148.4; 397 B per row | 3188 / 3125 / 1000; 438 B per row |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer | pkg-net10 | 215.0 | 216.3 | 1.006 | 496.1 / 496.1 / 496.1; 519 B per row | 3062 / 2969 / 1000; 517 B per row |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer | net10 | 212.5 | 214.0 | 1.007 | 496.1 / 496.1 / 496.1; 519 B per row | 3062 / 2969 / 1000; 517 B per row |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer | net11-ra-on | 214.1 | 229.8 | 1.073 | 496.1 / 496.1 / 496.1; 519 B per row | 3062 / 2969 / 1000; 517 B per row |
| FixedWidthWriteBenchmarks.WriteStream_Null | pkg-net10 | 94.5 | 95.0 | 1.005 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| FixedWidthWriteBenchmarks.WriteStream_Null | net10 | 97.3 | 97.9 | 1.007 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| FixedWidthWriteBenchmarks.WriteStream_Null | net11-ra-on | 94.1 | 94.9 | 1.008 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| FixedWidthWriteBenchmarks.WriteString | pkg-net10 | 110.4 | 112.0 | 1.015 | 201.2 / 201.2 / 201.2; 164 B per row | 187.5 / 187.5 / 187.5; 164 B per row |
| FixedWidthWriteBenchmarks.WriteString | net10 | 105.9 | 109.4 | 1.033 | 201.2 / 201.2 / 201.2; 164 B per row | 187.5 / 187.5 / 187.5; 164 B per row |
| FixedWidthWriteBenchmarks.WriteString | net11-ra-on | 202.5 | 143.9 | 0.711 | 246.1 / 246.1 / 246.1; 164 B per row | 234.4 / 234.4 / 234.4; 164 B per row |
| MultilineBenchmarks.ReadFile_ParseAndDiscard | pkg-net10 | 141.9 | 143.6 | 1.012 | 105.5 / 15.62 / -; 179 B per row | 1078 / 156.2 / -; 181 B per row |
| MultilineBenchmarks.ReadFile_ParseAndDiscard | net10 | 139.3 | 140.9 | 1.011 | 105.5 / 15.62 / -; 179 B per row | 1078 / 156.2 / -; 181 B per row |
| MultilineBenchmarks.ReadFile_ParseAndDiscard | net11-ra-on | 135.3 | 136.1 | 1.005 | 105.5 / 15.62 / -; 179 B per row | 1078 / 156.2 / -; 181 B per row |
| OptimizedParseBenchmarks.ReadFile | pkg-net10 | 777.5 | 1590.1 | 2.045 | 453.1 / 421.9 / -; 784 B per row | 6000 / 5750 / 1500; 793 B per row |
| OptimizedParseBenchmarks.ReadFile | net10 | 770.1 | 1546.2 | 2.008 | 453.1 / 421.9 / -; 784 B per row | 6000 / 5750 / 1500; 793 B per row |
| OptimizedParseBenchmarks.ReadFile | net11-ra-on | 752.5 | 1543.6 | 2.051 | 453.1 / 421.9 / -; 784 B per row | 6000 / 5750 / 1500; 793 B per row |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard | pkg-net10 | 778.0 | 768.7 | 0.988 | 453.1 / 78.12 / -; 778 B per row | 4571 / 142.9 / -; 788 B per row |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard | net10 | 792.2 | 764.2 | 0.965 | 453.1 / 78.12 / -; 778 B per row | 4571 / 142.9 / -; 788 B per row |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard | net11-ra-on | 708.7 | 732.3 | 1.033 | 437.5 / 62.5 / -; 778 B per row | 4571 / 142.9 / -; 787 B per row |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 259.0 | 262.2 | 1.012 | 140.6 / 19.53 / -; 236 B per row | 1375 / 156.2 / -; 231 B per row |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard | net10 | 253.2 | 255.3 | 1.008 | 140.6 / 19.53 / -; 236 B per row | 1375 / 156.2 / -; 231 B per row |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 251.1 | 253.0 | 1.008 | 140.6 / 19.53 / -; 236 B per row | 1375 / 156.2 / -; 231 B per row |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers | pkg-net10 | 277.8 | 473.4 | 1.704 | 144.5 / 125 / -; 244 B per row | 1875 / 1812 / 625; 239 B per row |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers | net10 | 276.6 | 470.6 | 1.701 | 144.5 / 125 / -; 244 B per row | 1875 / 1812 / 625; 239 B per row |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers | net11-ra-on | 269.2 | 475.7 | 1.767 | 144.5 / 121.1 / -; 244 B per row | 1875 / 1812 / 625; 239 B per row |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 245.3 | 247.2 | 1.008 | 140.6 / 19.53 / -; 235 B per row | 1375 / 156.2 / -; 231 B per row |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard | net10 | 238.1 | 238.9 | 1.003 | 140.6 / 19.53 / -; 235 B per row | 1375 / 156.2 / -; 231 B per row |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 237.7 | 239.7 | 1.008 | 140.6 / 19.53 / -; 235 B per row | 1375 / 156.2 / -; 231 B per row |
| QuotedWriteBenchmarks.WriteStream_Null | pkg-net10 | 116.7 | 118.2 | 1.013 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| QuotedWriteBenchmarks.WriteStream_Null | net10 | 113.6 | 114.6 | 1.009 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| QuotedWriteBenchmarks.WriteStream_Null | net11-ra-on | 113.2 | 113.8 | 1.005 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| QuotedWriteBenchmarks.WriteString | pkg-net10 | 175.4 | 170.2 | 0.970 | 248 / 248 / 248; 122 B per row | 281.2 / 281.2 / 281.2; 123 B per row |
| QuotedWriteBenchmarks.WriteString | net10 | 174.9 | 173.3 | 0.991 | 248 / 248 / 248; 122 B per row | 281.2 / 281.2 / 281.2; 123 B per row |
| QuotedWriteBenchmarks.WriteString | net11-ra-on | 172.7 | 168.0 | 0.973 | 248 / 248 / 248; 122 B per row | 281.2 / 281.2 / 281.2; 123 B per row |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard | pkg-net10 | 156.2 | 155.0 | 0.992 | 119.1 / 19.53 / -; 202 B per row | 1172 / 156.2 / -; 197 B per row |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard | net10 | 152.0 | 151.8 | 0.999 | 119.1 / 19.53 / -; 202 B per row | 1172 / 156.2 / -; 197 B per row |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard | net11-ra-on | 150.1 | 149.7 | 0.997 | 119.1 / 19.53 / -; 202 B per row | 1172 / 156.2 / -; 197 B per row |
| WriteBenchmarks.WriteFile_FileHelpers | pkg-net10 | 3420.8 | 4668.6 | 1.365 | - / - / -; 9 B per row | - / - / -; 1 B per row |
| WriteBenchmarks.WriteFile_FileHelpers | net10 | 5539.9 | 2593.7 | 0.468 | - / - / -; 9 B per row | - / - / -; 1 B per row |
| WriteBenchmarks.WriteFile_FileHelpers | net11-ra-on | 5390.9 | 4653.1 | 0.863 | - / - / -; 9 B per row | - / - / -; 1 B per row |
| WriteBenchmarks.WriteString_FileHelpers | pkg-net10 | 698.0 | 696.4 | 0.998 | 328.1 / 328.1 / 328.1; 571 B per row | 142.9 / 142.9 / 142.9; 581 B per row |
| WriteBenchmarks.WriteString_FileHelpers | net10 | 692.3 | 688.3 | 0.994 | 328.1 / 328.1 / 328.1; 571 B per row | 142.9 / 142.9 / 142.9; 581 B per row |
| WriteBenchmarks.WriteString_FileHelpers | net11-ra-on | 651.9 | 642.1 | 0.985 | 328.1 / 328.1 / 328.1; 571 B per row | 250 / 250 / 250; 581 B per row |

### `fh-intel`

| Benchmark | Build | 10K rows: ns per row | 100K rows: ns per row | 100K/10K | 10K: Gen0 / Gen1 / Gen2; allocated | 100K: Gen0 / Gen1 / Gen2; allocated |
|---|---|---|---|---|---|---|
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord | pkg-net10 | 309.3 | 311.7 | 1.008 | 62.5 / - / -; 169 B per row | 687.5 / - / -; 176 B per row |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord | net10 | 302.7 | 307.7 | 1.016 | 62.5 / - / -; 169 B per row | 687.5 / - / -; 176 B per row |
| AsyncStreamingBenchmarks.ReadAsync_MultiRecord | net11-ra-on | 285.6 | 286.3 | 1.002 | 66.41 / - / -; 169 B per row | 687.5 / - / -; 176 B per row |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard | pkg-net10 | 742.7 | 753.2 | 1.014 | 304.7 / - / -; 771 B per row | 3000 / - / -; 785 B per row |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard | net10 | 742.8 | 747.7 | 1.007 | 304.7 / - / -; 771 B per row | 3000 / - / -; 785 B per row |
| DelimitedParseBenchmarks.CsvHelper_ParseAndDiscard | net11-ra-on | 703.0 | 714.9 | 1.017 | 304.7 / - / -; 771 B per row | 3000 / - / -; 785 B per row |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 757.5 | 772.9 | 1.020 | 296.9 / 46.88 / -; 776 B per row | 3000 / 142.9 / -; 785 B per row |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard | net10 | 755.7 | 769.8 | 1.019 | 296.9 / 46.88 / -; 776 B per row | 3000 / 142.9 / -; 785 B per row |
| DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 736.9 | 753.9 | 1.023 | 304.7 / 54.69 / -; 776 B per row | 3000 / 142.9 / -; 785 B per row |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard | pkg-net10 | 548.1 | 565.6 | 1.032 | 304.7 / 54.69 / -; 777 B per row | 3111 / 111.1 / -; 785 B per row |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard | net10 | 541.7 | 558.2 | 1.031 | 304.7 / 54.69 / -; 777 B per row | 3111 / 111.1 / -; 785 B per row |
| DelimitedParseBenchmarks.Sylvan_ParseAndDiscard | net11-ra-on | 528.5 | 544.6 | 1.030 | 304.7 / 54.69 / -; 777 B per row | 3100 / 200 / -; 785 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path | pkg-net10 | 923.8 | 917.4 | 0.993 | 296.9 / - / -; 776 B per row | 3000 / - / -; 790 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path | net10 | 907.0 | 917.9 | 1.012 | 296.9 / - / -; 776 B per row | 3000 / - / -; 790 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path | net11-ra-on | 856.1 | 871.0 | 1.017 | 296.9 / - / -; 780 B per row | 3000 / - / -; 794 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 | pkg-net10 | 958.4 | 970.4 | 1.013 | 312.5 / 46.88 / -; 792 B per row | 3167 / 166.7 / -; 806 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 | net10 | 943.1 | 958.4 | 1.016 | 312.5 / 46.88 / -; 792 B per row | 3167 / 166.7 / -; 806 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Path_ExceptionEvery100 | net11-ra-on | 936.9 | 973.9 | 1.040 | 312.5 / 62.5 / -; 796 B per row | 3167 / 166.7 / -; 810 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_Stream | net11-ra-on | 747.3 | 761.2 | 1.019 | 304.7 / - / -; 771 B per row | 3000 / - / -; 785 B per row |
| EngineAsyncBenchmarks.Engine_ReadAsync_TextReader | net11-ra-on | 741.6 | 760.7 | 1.026 | 304.7 / - / -; 771 B per row | 3000 / - / -; 785 B per row |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback | pkg-net10 | 849.7 | 850.0 | 1.000 | 296.9 / 46.88 / -; 778 B per row | 3000 / 166.7 / -; 788 B per row |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback | net10 | 829.7 | 833.1 | 1.004 | 296.9 / 46.88 / -; 778 B per row | 3000 / 166.7 / -; 788 B per row |
| EngineAsyncBenchmarks.Engine_ReadFileAsync_Callback | net11-ra-on | 825.6 | 817.9 | 0.991 | 296.9 / 46.88 / -; 778 B per row | 3000 / 142.9 / -; 787 B per row |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null | net11-ra-on | 509.0 | 544.0 | 1.069 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows | net11-ra-on | 82.0 | 84.5 | 1.030 | 5.859 / - / -; 17 B per row | 62.5 / - / -; 17 B per row |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 134.3 | 137.2 | 1.022 | 164.1 / 25.39 / -; 413 B per row | 1625 / 156.2 / -; 409 B per row |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard | net10 | 131.1 | 134.0 | 1.023 | 164.1 / 25.39 / -; 413 B per row | 1625 / 156.2 / -; 409 B per row |
| FixedWidthParseBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 132.9 | 133.5 | 1.005 | 164.1 / 25.39 / -; 413 B per row | 1625 / 156.2 / -; 409 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Class | pkg-net10 | 186.9 | 542.7 | 2.903 | 166 / 154.3 / -; 421 B per row | 2312 / 2250 / 750; 417 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Class | net10 | 184.8 | 538.8 | 2.916 | 166 / 154.3 / -; 421 B per row | 2312 / 2250 / 750; 417 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Class | net11-ra-on | 175.9 | 534.7 | 3.041 | 166 / 156.2 / -; 421 B per row | 2312 / 2250 / 750; 417 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Struct | pkg-net10 | 259.4 | 504.5 | 1.945 | 164.1 / 164.1 / 164.1; 397 B per row | 2438 / 2375 / 937.5; 438 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Struct | net10 | 253.5 | 501.0 | 1.976 | 164.1 / 164.1 / 164.1; 397 B per row | 2438 / 2375 / 937.5; 438 B per row |
| FixedWidthParseBenchmarks.ReadAllRows_Struct | net11-ra-on | 247.5 | 500.4 | 2.022 | 164.1 / 164.1 / 164.1; 397 B per row | 2438 / 2375 / 937.5; 438 B per row |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer | pkg-net10 | 213.7 | 305.6 | 1.430 | 496.1 / 496.1 / 496.1; 519 B per row | 2250 / 2219 / 906.2; 517 B per row |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer | net10 | 213.8 | 305.2 | 1.427 | 496.1 / 496.1 / 496.1; 519 B per row | 2250 / 2219 / 906.2; 517 B per row |
| FixedWidthParseBenchmarks.SliceAllFields_SpanSlicer | net11-ra-on | 206.8 | 284.7 | 1.377 | 496.1 / 496.1 / 496.1; 519 B per row | 2250 / 2219 / 906.2; 517 B per row |
| FixedWidthWriteBenchmarks.WriteStream_Null | pkg-net10 | 89.0 | 92.3 | 1.037 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| FixedWidthWriteBenchmarks.WriteStream_Null | net10 | 91.3 | 95.2 | 1.043 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| FixedWidthWriteBenchmarks.WriteStream_Null | net11-ra-on | 82.8 | 88.3 | 1.066 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| FixedWidthWriteBenchmarks.WriteString | pkg-net10 | 112.6 | 122.9 | 1.092 | 200.2 / 200.2 / 200.2; 164 B per row | 187.5 / 187.5 / 187.5; 164 B per row |
| FixedWidthWriteBenchmarks.WriteString | net10 | 111.0 | 123.3 | 1.111 | 201.2 / 201.2 / 201.2; 164 B per row | 187.5 / 187.5 / 187.5; 164 B per row |
| FixedWidthWriteBenchmarks.WriteString | net11-ra-on | 139.9 | 144.3 | 1.031 | 201.2 / 201.2 / 201.2; 164 B per row | 187.5 / 187.5 / 187.5; 164 B per row |
| MultilineBenchmarks.ReadFile_ParseAndDiscard | pkg-net10 | 146.9 | 148.9 | 1.014 | 70.31 / 9.766 / -; 179 B per row | 718.8 / 109.4 / -; 181 B per row |
| MultilineBenchmarks.ReadFile_ParseAndDiscard | net10 | 144.2 | 146.8 | 1.018 | 70.31 / 9.766 / -; 179 B per row | 718.8 / 109.4 / -; 181 B per row |
| MultilineBenchmarks.ReadFile_ParseAndDiscard | net11-ra-on | 146.9 | 147.1 | 1.001 | 70.31 / 9.766 / -; 179 B per row | 718.8 / 109.4 / -; 181 B per row |
| OptimizedParseBenchmarks.ReadFile | pkg-net10 | 870.1 | 1490.6 | 1.713 | 296.9 / 250 / -; 784 B per row | 4000 / 3750 / 1000; 793 B per row |
| OptimizedParseBenchmarks.ReadFile | net10 | 855.9 | 1470.2 | 1.718 | 296.9 / 250 / -; 784 B per row | 4000 / 3750 / 1000; 793 B per row |
| OptimizedParseBenchmarks.ReadFile | net11-ra-on | 834.8 | 1449.2 | 1.736 | 296.9 / 250 / -; 784 B per row | 4000 / 3750 / 1000; 793 B per row |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard | pkg-net10 | 854.0 | 844.1 | 0.988 | 296.9 / 46.88 / -; 778 B per row | 3000 / 166.7 / -; 788 B per row |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard | net10 | 834.3 | 829.2 | 0.994 | 296.9 / 46.88 / -; 778 B per row | 3000 / 166.7 / -; 788 B per row |
| OptimizedParseBenchmarks.ReadFileAsync_Callback_ProcessAndDiscard | net11-ra-on | 819.0 | 818.3 | 0.999 | 250 / - / -; 778 B per row | 3000 / 142.9 / -; 787 B per row |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 282.6 | 286.5 | 1.014 | 93.75 / 11.72 / -; 236 B per row | 906.2 / 125 / -; 231 B per row |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard | net10 | 279.4 | 283.3 | 1.014 | 93.75 / 11.72 / -; 236 B per row | 906.2 / 125 / -; 231 B per row |
| QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 274.5 | 278.9 | 1.016 | 93.75 / 11.72 / -; 236 B per row | 906.2 / 125 / -; 231 B per row |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers | pkg-net10 | 302.9 | 493.1 | 1.628 | 93.75 / 78.12 / -; 244 B per row | 1250 / 1188 / 375; 239 B per row |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers | net10 | 302.9 | 491.7 | 1.623 | 93.75 / 78.12 / -; 244 B per row | 1250 / 1188 / 375; 239 B per row |
| QuotedFieldsBenchmarks.ReadAllRows_FileHelpers | net11-ra-on | 293.4 | 487.0 | 1.660 | 93.75 / 78.12 / -; 244 B per row | 1250 / 1188 / 375; 239 B per row |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard | pkg-net10 | 258.7 | 261.9 | 1.012 | 93.75 / 11.72 / -; 235 B per row | 906.2 / 125 / -; 231 B per row |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard | net10 | 257.6 | 261.2 | 1.014 | 93.75 / 11.72 / -; 235 B per row | 906.2 / 125 / -; 231 B per row |
| QuotedFieldsPeriodicBenchmarks.FileHelpers_ParseAndDiscard | net11-ra-on | 255.4 | 259.6 | 1.016 | 93.75 / 11.72 / -; 235 B per row | 906.2 / 125 / -; 231 B per row |
| QuotedWriteBenchmarks.WriteStream_Null | pkg-net10 | 109.6 | 113.9 | 1.039 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| QuotedWriteBenchmarks.WriteStream_Null | net10 | 114.0 | 118.2 | 1.036 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| QuotedWriteBenchmarks.WriteStream_Null | net11-ra-on | 109.1 | 112.0 | 1.027 | - / - / -; 0 B per row | - / - / -; 0 B per row |
| QuotedWriteBenchmarks.WriteString | pkg-net10 | 172.0 | 175.5 | 1.020 | 248 / 248 / 248; 122 B per row | 281.2 / 281.2 / 281.2; 123 B per row |
| QuotedWriteBenchmarks.WriteString | net10 | 172.7 | 176.5 | 1.022 | 248 / 248 / 248; 122 B per row | 281.2 / 281.2 / 281.2; 123 B per row |
| QuotedWriteBenchmarks.WriteString | net11-ra-on | 168.8 | 172.8 | 1.024 | 248 / 248 / 248; 122 B per row | 281.2 / 281.2 / 281.2; 123 B per row |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard | pkg-net10 | 163.9 | 165.4 | 1.009 | 80.08 / 11.72 / -; 202 B per row | 781.2 / 125 / -; 197 B per row |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard | net10 | 163.2 | 165.2 | 1.012 | 80.08 / 11.72 / -; 202 B per row | 781.2 / 125 / -; 197 B per row |
| SparseQuoteBenchmarks.ReadFile_ParseAndDiscard | net11-ra-on | 156.7 | 159.0 | 1.015 | 80.08 / 11.72 / -; 202 B per row | 781.2 / 125 / -; 197 B per row |
| WriteBenchmarks.WriteFile_FileHelpers | pkg-net10 | 1633.5 | 1493.8 | 0.915 | - / - / -; 9 B per row | - / - / -; 1 B per row |
| WriteBenchmarks.WriteFile_FileHelpers | net10 | 1625.6 | 1556.1 | 0.957 | - / - / -; 9 B per row | - / - / -; 1 B per row |
| WriteBenchmarks.WriteFile_FileHelpers | net11-ra-on | 1626.9 | 1430.1 | 0.879 | - / - / -; 9 B per row | - / - / -; 1 B per row |
| WriteBenchmarks.WriteString_FileHelpers | pkg-net10 | 726.5 | 760.4 | 1.047 | 328.1 / 328.1 / 328.1; 571 B per row | 142.9 / 142.9 / 142.9; 581 B per row |
| WriteBenchmarks.WriteString_FileHelpers | net10 | 728.9 | 768.0 | 1.054 | 328.1 / 328.1 / 328.1; 571 B per row | 142.9 / 142.9 / 142.9; 581 B per row |
| WriteBenchmarks.WriteString_FileHelpers | net11-ra-on | 687.8 | 733.6 | 1.067 | 328.1 / 328.1 / 328.1; 571 B per row | 142.9 / 142.9 / 142.9; 581 B per row |

Explanation: the rows whose time per row grows from 10K to 100K are the ones that keep every record alive. On
`fh-amd`: `OptimizedParseBenchmarks.ReadFile` 2.008 to 2.051, `FixedWidthParseBenchmarks.ReadAllRows_Class` 2.931 to
2.983, `ReadAllRows_Struct` 1.250 to 1.294 and `QuotedFieldsBenchmarks.ReadAllRows_FileHelpers` 1.701 to 1.767, with
Gen2 collections at 100K rows (1500, 1125, 1000 and 625 per 1,000 operations) where 10K rows has none or fewer (148.4
for the struct read). The parse-and-discard reads stay at 1.005 to 1.025 with no Gen2 collection, as CsvHelper does
(0.996 to 1.011). The 4.9.1 package and the 4.10 builds have the same counts, so the growth is in collecting the
result, not in the per-row parse code; no number of this round separates the two further.

## GC and allocation

Gen0, Gen1 and Gen2 (collections per 1,000 operations) and bytes allocated per operation of every BenchmarkDotNet case
and build (design 3.1 B1), from the BenchmarkDotNet reports in the `*.runs` folders; the median over the launches. The
full table (about 450 rows per VM) is the "GC and allocation" section of `round-report.py` on the round's folder with
the `*.runs` folders of the archives in place; it is not copied here. The scenario tables of v4.10.0.md carry the GC
columns of the lines the gates read.

## Findings F1 and F2

Design 3.6 lists two write findings for T57, both measured in the round above:

| Finding | Step 0 result | Outcome in the 4.10 rounds |
|---|---|---|
| F1: fixed-width `WriteString` 1.17 to 1.68 times slower on .NET 11 than on .NET 10 for the same 4.9.1 IL (free-gains pair, both VMs; cause: the .NET 11 Tier1 inliner, dotnet/runtime#137552) | free-gains tables above | T57: `net11-ra-on` / `net10` on the T19 commit 0.871 (`fh-amd`) and 0.914 (`fh-intel`), faster (round `g1-2026-11-16`); on the round-2 commit 1.050 and 1.009 at 100K, no change, with 1.054 slower on `fh-intel` at 10K (round `write-2026-11-17`) |
| F2: `v410-net10` fixed-width `WriteStream_Null` 3.2 to 3.8 % slower than 4.9.1 | `fh-amd` 1.032 [1.014, 1.041] (10K) and 1.035 [1.013, 1.041] (100K), slower | gone: 0.581 (`fh-amd`) and 0.738 (`fh-intel`) at 100K, faster (round `diagFW2-2026-11-19`) |

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

Outcome in the 4.10 rounds (v4.10.0.md): no Tier1 listing from the VMs is recorded, so the verdicts stay open and none
is `runtime`. F3 and F4 led to T22's direct-call shape, which the T19 and T25 rounds confirm (`Format_WriterDirectCall`
against `Format_GenericWriter` is never 3 % slower by B4 on both runtimes). F5 changes nothing in `ScanSingleQuote`
(T33 step 2), and the T21 round keeps `UseUnsafeLoad` `false` on both runtimes. F6: the T30 gate closed with
`Slice_Direct` against `Slice_MathMin` at no change on both runtimes and VMs (round `g1-2026-11-16`).

## Not run

Globs of the filter file that selected nothing in one build of a pair (from `not-run.txt`), and methods that `ab.py`
found in one build of a pair only (`only_in_a`, `only_in_b`): none on either VM.

## Failed

`ab.py` runs that failed (from `failed.txt`): none on either VM.

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

The report of this page: `python3 -I benchmarks/tools/round-report.py docs/benchmarks/raw/step0-2026-11-12` (without
the `*.runs` folders the GC and allocation columns are empty).
