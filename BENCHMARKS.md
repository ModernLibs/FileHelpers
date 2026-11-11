# Benchmarks

The `benchmarks/ModernLibs.FileHelpers.Benchmarks` project is a
[BenchmarkDotNet](https://benchmarkdotnet.org/) suite. It measures ModernLibs.FileHelpers on its own and
against FileHelpers 3.5.2, Sep, Sylvan.Data.Csv and CsvHelper, with time and allocations
(`[MemoryDiagnoser]`) for every benchmark.

## Results

**[docs/benchmarks/v4.8.0.md](docs/benchmarks/v4.8.0.md)** has the 4.8.0 results (AMD Ryzen 9 5900X,
Ubuntu 26.04 under WSL2, .NET 10.0.13, BenchmarkDotNet default job). Headlines, at 100K rows:

| Scenario | ModernLibs.FileHelpers 4.8.0 | Others |
|---|---|---|
| Read a 25-column delimited file into objects | 182 to 191 ms, 186 MB | FileHelpers 3.5.2: 344 ms, 231 MB; Sylvan: 107 ms, 76 MB; Sep: 122 ms, 76 MB; CsvHelper: 352 ms, 155 MB |
| Read a fixed-width file (7 fields, class record) | 53 ms, 71 MB | FileHelpers 3.5.2: 84 ms, 59 MB |
| Read a file with quoted fields | 49 ms, 44 MB | Sep: 22 ms; Sylvan: 28 ms; CsvHelper: 78 ms |
| Write the 25-column records to a string | 70 ms, 111 MB | Sep: 75 ms, 111 MB; CsvHelper: 173 ms, 164 MB |

4.8 is about twice as fast as FileHelpers 3.5.2 on these reads and faster than CsvHelper 33.0.1 (with
reflection mapping) everywhere measured, writes as fast as Sep, but reads slower than Sep and Sylvan. `ReadFile(path)` to an array
allocates about 2.5 times more than Sep or Sylvan because it loads the whole file into a string; the
streaming `ReadFile(path, callback)` overload allocates the same as Sep. The results page lists the
caveats (WSL2, what each library does per row).

**[docs/benchmarks/v4.8.1.md](docs/benchmarks/v4.8.1.md)** compares 4.8.1 with 4.8.0 on the same
machine: reads are 8 to 26 % faster and `WriteFile` 12 to 18 % faster, with the same allocation per
record.

**[docs/benchmarks/v4.9.0.md](docs/benchmarks/v4.9.0.md)** compares 4.9.0 with 4.8.1 on nine suites,
on an Azure virtual machine (AMD EPYC 7763, Ubuntu 24.04, not WSL), so its times are not comparable
with the 4.8.0 and 4.8.1 pages. `ReadFile(path)` to an array no longer loads the file into a string:
it is 28 to 52 % faster and allocates as much as Sep and Sylvan (100K rows of the 25-column file:
about 154 ms and 76 MB, instead of 216 to 243 ms and 186 MB). The quoted file reads 27 to 44 % faster
and `WriteString` 17 to 30 % faster, both with about half the allocation. Fixed-width reads allocate 43 to
45 % less; class records are 17 to 43 % faster, struct records faster at 10K rows and 14 % slower at
100K rows. These runs use Sep 0.17.1, Sylvan.Data.Csv 1.4.4 and CsvHelper 34.0.0 with
source-generated records, so their CsvHelper numbers are not comparable with the CsvHelper 33.0.1
numbers above. In them the 25-column read is faster than Sep and CsvHelper at every size and slower
than Sylvan at 10K and 100K rows (equal at 1M); `WriteString` is faster than Sep and slower than
CsvHelper; Sep, Sylvan and CsvHelper still read the quoted file faster. The 4.8.0 numbers above are
kept as the baseline against FileHelpers 3.5.2.

Performance depends heavily on hardware, record shape and field types, so run the suite on a machine
like your production one.

[docs/benchmarks/v4.0.0.md](docs/benchmarks/v4.0.0.md) holds an early development snapshot. It was
recorded before the 4.3.0 milestone and is kept for history only. Its numbers are not comparable:
before 4.8.0 the benchmark records parsed headers and dates incorrectly (conversion failures were
silent) and the "FileHelpers 3.x" records actually used this library.

When results are published, each file in `docs/benchmarks/` states the library version and commit,
the date, the hardware, OS, .NET SDK and runtime, and the BenchmarkDotNet version.

## Running

Always run in Release on an otherwise idle machine. The project targets `net10.0` and `net11.0`, so
`dotnet run` needs `-f`:

```bash
# everything (takes a long time)
dotnet run -c Release -f net11.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'

# one class
dotnet run -c Release -f net11.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*FixedWidthComparisonBenchmarks*'

# quicker, less precise (Default, Dry, Short, Medium, Long, VeryLong)
dotnet run -c Release -f net10.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*SepComparison*' --job short

# one benchmark method in a process of its own (see below)
dotnet run -c Release -f net11.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- \
  --filter 'ModernLibs.FileHelpers.Benchmarks.Benchmarks.SepComparisonBenchmarks.FileHelpers_ParseAndDiscard'
```

BenchmarkDotNet writes reports to `BenchmarkDotNet.Artifacts/` in the current directory (`--artifacts
<dir>` changes it): markdown, CSV and HTML tables and the full JSON report (`*-report-full.json`).
Every table has the columns Gen0, Gen1, Gen2 (collections per 1,000 operations; `-` is zero) and
Allocated, also when a count is zero for every row.

**The benchmarks run in-process** (`BenchConfig`). BenchmarkDotNet's usual toolchain does not run the
assembly it is started from: it finds the project file under the current directory and builds it again
with default properties, which would turn a `net11-ra-off` build back into the default runtime-async
build (and BenchmarkDotNet 0.15.8 does not recognise .NET 11 there). In-process, the measured code is
the build that was started. There is no silent way back to the usual toolchain: a run in which any
benchmark would use another toolchain (for example through `--coreRun` or a job attribute) stops with a
validation error and exit code 1, `--runtimes` is refused, `--inProcess` is accepted and changes
nothing, and `--job <name>` keeps the in-process toolchain.

Every process prints, before BenchmarkDotNet starts, which library it measures:

```text
// ModernLibs.FileHelpers variant: tfm=net11.0 runtime-async=on
// ModernLibs.FileHelpers assembly: /home/me/fh-bench/net11-ra-on/ModernLibs.FileHelpers.dll
// Runtime: .NET 11.0.0
```

Running in-process has costs, which every result has to live with:

- **One process for everything a filter selects.** All benchmarks selected by one `--filter` run in the
  same process, one after the other, and what one leaves behind (for example buffers in the shared
  array pools) can change the garbage collector's timing in the next. Numbers that are published or
  compared come from one benchmark method per process, which is what `ab.py` does (below). Even then
  the `Rows` values of one method share that process. A filter that selects several methods is fine
  for a look at the data, not for a result.
- **No per-job process settings.** The environment variables and garbage-collector settings of a
  BenchmarkDotNet job (server or concurrent GC, `--envVars`, ...) cannot apply: the process already
  runs. They come from the process that starts the benchmark. If a round ever needs them, the
  launcher (`ab.py`) has to set them for each process, for example as `DOTNET_gcServer=1`.
- **No `--runtimes`.** A process runs on one runtime; build one folder per configuration instead.

## What is measured

| Class | Measures | Compared with | Rows |
|---|---|---|---|
| `DelimitedComparisonBenchmarks` | Reading a 25-column delimited file into an array | FileHelpers 3.5.2 | 10K, 100K |
| `FixedWidthComparisonBenchmarks` | Reading a fixed-width file (class and struct records) | FileHelpers 3.5.2 | 10K, 100K |
| `DelimitedParseBenchmarks` | Reading all rows, and only the first column, of the 25-column file | Sylvan.Data.Csv, CsvHelper | 10K, 100K, 1M |
| `SepComparisonBenchmarks` | Reading all rows and writing them back to a string | Sep | 10K, 100K, 1M |
| `QuotedFieldsBenchmarks` | Reading a file with quoted fields (embedded delimiters, doubled quotes); from 4.10 on the seeded random set, so its numbers are not comparable with the 4.8 and 4.9 quoted results | Sylvan.Data.Csv, CsvHelper, Sep | 10K, 100K |
| `QuotedFieldsPeriodicBenchmarks` | The same on the periodic quoted file (row i uses pattern i % 10); continues the 4.8 and 4.9 quoted series | Sylvan.Data.Csv, CsvHelper, Sep | 10K, 100K |
| `SparseQuoteBenchmarks` | `ReadFile` and `ReadFile_ParseAndDiscard` on the quoted layout where 1 % of the rows contain a quote | - | 10K, 100K |
| `LongLineBenchmarks` | The same on 8-column lines of 505 characters | - | 5K, 20K |
| `MultilineBenchmarks` | The same where every tenth row has a quoted value with a line break | - | 10K, 100K |
| `WideRecordBenchmarks` | The same on 150 columns (75 int, 75 string) | - | 1K, 10K |
| `QuotedWriteBenchmarks` | `WriteString` and `WriteStream_Null` (to `TextWriter.Null`) of the seeded quoted records | - | 10K, 100K |
| `FixedWidthWriteBenchmarks` | `WriteString` and `WriteStream_Null` of the fixed-width records | - | 10K, 100K |
| `WriteBenchmarks` | `WriteString` and `WriteFile` | CsvHelper | 10K, 100K, 1M |
| `FixedWidthParseBenchmarks` | Fixed-width reads, class versus struct records | CsvHelper | 10K, 100K |
| `OptimizedParseBenchmarks` | `ReadFile`, `ReadString`, `ReadSpan` and their callback and async variants | - | 10K, 100K |
| `AsyncStreamingBenchmarks` | `MultiRecordAsyncEngine.ReadAsync` versus `MultiRecordEngine.ReadFile`, with and without cancellation | - | 10K, 100K |

Competitor versions are pinned in `Directory.Packages.props`: FileHelpers 3.5.2, Sep 0.17.1,
Sylvan.Data.Csv 1.4.4, CsvHelper 34.0.0, BenchmarkDotNet 0.15.8. The 4.8.0 and 4.8.1 results were
recorded with Sep 0.14.1, Sylvan.Data.Csv 1.4.0 and CsvHelper 33.0.1.

Note that the libraries do different amounts of work: ModernLibs.FileHelpers and FileHelpers 3.x
produce typed records with converters and validation, while the CSV readers are measured with the
row-to-object code written in each benchmark. Compare like with like.

**Parse-and-discard.** Each class that compares libraries (`DelimitedParse`, `SepComparison`,
`QuotedFields`, `FixedWidthParse`, `DelimitedComparison`, `FixedWidthComparison`) has one method
`<Library>_ParseAndDiscard` per library, with the library name the class uses elsewhere
(`FileHelpers`, `Sylvan`, `CsvHelper`, `Sep`; `Modern` and `Legacy` in the two classes that compare
with FileHelpers 3.5.2). Each one parses every record into the same record or row object as the
class's `ReadAllRows` methods, adds one field to a checksum and keeps nothing: ModernLibs.FileHelpers
through `ReadFile(path, callback)`, FileHelpers 3.5.2 through `FileHelperAsyncEngine.ReadNext`, the
other libraries through their own read loop. The checksum is the sum of the `Id` column (delimited
files) or of the last character of the `Id` field (fixed-width files); each method's setup compares it
with the value computed from the dataset. Without the array or list that holds every record, these
methods measure parsing on its own; they are the primary metric of the 4.10 read work (design B2).

## Configurations and A/B runs

From 4.10 on, the library is measured in three named configurations (design D6). The names are used as
they stand in result tables and file names:

| Name | Target and runtime | Runtime async | Build |
|---|---|---|---|
| `net10` | `net10.0` on .NET 10 | does not exist | `-f net10.0` |
| `net11-ra-on` | `net11.0` on .NET 11 | on | `-f net11.0` (what ships) |
| `net11-ra-off` | `net11.0` on .NET 11 | off | `-f net11.0 -p:FileHelpersRuntimeAsync=false` |

`benchmarks/tools/build-variants.sh <out-dir>` builds the benchmark project three times, into
`<out-dir>/net10`, `<out-dir>/net11-ra-on` and `<out-dir>/net11-ra-off`, each with its own output and
intermediate folders. After each build it runs `verify-variant` (`benchmarks/tools/verify-variant`) on
the library DLL of that folder. The tool reads the DLL's metadata, without loading it, and prints the
target framework and whether `FileHelperEngine<T>.ReadFileAsync` carries the runtime-async flag
(`0x2000`); the script stops unless that matches the folder's name, and writes the line, the commit and
the SDK version to `<out-dir>/<name>/variant.txt`. The expected output:

```text
net10: tfm=net10.0 runtime-async=off
net11-ra-on: tfm=net11.0 runtime-async=on
net11-ra-off: tfm=net11.0 runtime-async=off
```

Run a configuration with its DLL, from any directory:

```bash
benchmarks/tools/build-variants.sh ~/fh-bench
dotnet ~/fh-bench/net11-ra-on/ModernLibs.FileHelpers.Benchmarks.dll --filter '*DelimitedParse*ParseAndDiscard*'
```

**Interleaved A/B with `ab.py`.** `benchmarks/tools/ab.py` (Python 3, standard library only) compares
two built folders, for example two configurations, or a 4.9.1 build and a 4.10 build:

```bash
python3 -I benchmarks/tools/ab.py --a ~/fh-bench/net11-ra-off --b ~/fh-bench/net11-ra-on \
  --launches 10 --filter '*AsyncStreaming*' --out ra-off-vs-on.json
```

(The same command on `*ParseAndDiscard*` is in effect an A/A run: those benchmarks are synchronous, so
runtime async cannot change them. It is a useful check of the noise.)

It lists the benchmark methods that match `--filter` in both folders and runs each method `--launches`
times per side, alternately: A, B, A, B, ... Each run is one BenchmarkDotNet launch of one method, a
process of its own. Arguments after `--` go to BenchmarkDotNet (for example `-- --job short`); options
that measure nothing or that `ab.py` sets itself (`--list`, `--help`, `--info`, `--filter`,
`--artifacts`, ...) are refused. The raw BenchmarkDotNet output of every launch stays in
`<out>.runs/`. Each launch folder is emptied before its launch, and a launch that exits non-zero or
writes no report stops the run; the benchmark program exits 1 whenever it measured nothing. `ab.py`
also records the SHA-256 of each side's `ModernLibs.FileHelpers.dll` and stops when it changes during
the run.

Both folders must come from a committed tree: a `variant.txt` that says "uncommitted changes" is
refused unless `--allow-dirty` is given (for a local smoke run). `ab.py` warns when the two folders
come from different commits (legitimate, for example a 4.9.1 build against a 4.10 build) or when a
folder has no `variant.txt`; the warnings go into the JSON file.

Each process's start-up lines are checked. `ab.py` stops when the variant a process printed is not the
one its folder is named after (`net10`, `net11-ra-on`, `net11-ra-off`), when it loaded
ModernLibs.FileHelpers from another folder, when its .NET major version is not the configuration's, or
when the lines change between launches. A folder with another name, for example a 4.9.1 build, needs
the expected line stated: `--expect-a 'tfm=net10.0 runtime-async=off'`. The checked lines go into the
JSON file (`a.startup`, `b.startup`), next to the folder's `variant.txt`. One sample is the median time of one
launch; for each benchmark case (method and parameters) the JSON file holds the A and B samples, the
median ratio B/A (below 1.0: B is faster), its 99.9 % bootstrap interval (ratio of medians, 10,000
resamples, fixed seed 410) and a `verdict` of `faster`, `slower` or `no-change`.

The verdict applies **rule B4** of the 4.10 design, fixed before any round: a change counts when the
99.9 % interval of the ratio excludes 1.0 **and** the median ratio differs from 1.0 by at least 0.03
(3 %). A claim holds only when it holds on at least two Azure virtual machines, one of them the x64
AVX2 reference machine; that condition also limits false changes when a round compares dozens of
cases, each with its own small false-change rate.

**A B4 result needs at least 7 launches per side.** With few samples the interval is not a true
99.9 % interval: with no real difference it excludes 1.0 exactly when all B samples lie on one side of
all A samples, which happens with probability 2 / C(2n, n). For n = 5 that is 0.79 % (0.67 % in a
simulation of 1,500 runs), about 8 times the nominal 0.1 %; for n = 7 it is 0.058 % (0.07 % simulated),
within the nominal level. `ab.py` therefore refuses fewer than 7 launches. With `--smoke` it accepts 5
or 6; the JSON file then records `"b4": false` and the printed table starts with "smoke, not B4". A run
without `--smoke` records `"b4": true`. Run the tests of `ab.py` with
`python3 -I -m unittest discover -s benchmarks/tools -p 'test_*.py'`.

**Local numbers are smoke checks (design D1).** A run on a developer machine or under WSL answers
"does it run", "is the data right" and "is the direction plausible". Its numbers are not evidence and
go into no report, gate or changelog; results come from Azure rounds only.

## Datasets

The datasets are generated on the first run and reused afterwards. Each one is a pure function of its
row count (fixed seed, fixed base date), so every run and every machine reads the same bytes. They are
stored in a `Data` folder next to the compiled benchmark assembly (under `bin/`); the file names carry
a format version, bumped whenever the generated text changes.

- **PackageAssets**: delimited, 25 columns (up to 1M rows), a header line, ISO 8601 dates
  (`yyyy-MM-ddTHH:mm:ss`, read with `[FieldConverter(ConverterKind.Date, "s")]`) and empty text columns
- **Fixed-width**: 80-character records, 7 fields
- **Quoted fields**: delimited with a header line and RFC 4180 quoted values; the periodic file
  (`quoted_fields_v2_*`, pattern i % 10, read by `QuotedFieldsPeriodicBenchmarks`) and the seeded file
  (`quoted_random_v1_*`, seed 42, a random one of the same ten patterns per row, read by `QuotedFieldsBenchmarks`)
- **Sparse quotes** (`sparse_quotes_v1_*`): the quoted layout, quotes in 1 % of the rows
- **Long lines** (`long_lines_v1_*`): 8 columns, 505 characters per line, no header
- **Multiline values** (`multiline_v1_*`): every tenth row has a quoted value with one line break, no header
- **Wide records** (`wide_150_v1_*`): 150 columns, 75 int and 75 string, no header
- **Multi-record**: alternating `H|…` and `D|…` lines (generated by `AsyncStreamingBenchmarks`)
- **Floats**: numeric data (generator only; no benchmark reads it yet)

Each benchmark has its own `[GlobalSetup(Target = ...)]`. It runs that benchmark method once and
compares its result with the raw dataset (row count and sampled fields for reads, every line for
writes), and throws on a difference: a benchmark that parses its data wrongly fails instead of
reporting a number. The setup runs no other benchmark, because what another library or read leaves in
the process (for example pooled buffers) changes the garbage collector's timing and with it the
measured time. For the same reason, results come from one benchmark method per process (see
"Running").
