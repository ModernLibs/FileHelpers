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

**[docs/benchmarks/v4.10.0.md](docs/benchmarks/v4.10.0.md)** records the Azure rounds of 4.10: two
VMs (`Standard_D4ads_v5`, AMD, AVX2, the reference; `Standard_D4ds_v5`, Intel Xeon Platinum 8370C,
AVX-512), A/B against the published 4.9.1 package on .NET 10 and .NET 11 with 7 launches per side and
rule B4, runtime async on against off, and a comparison with other libraries. At 100K rows the
25-column parse and discard takes 0.79 to 0.80 of the 4.9.1 time, the quoted parse and discard 0.76, and
`WriteString` of the 25 columns 0.48 to 0.62, on both VMs and runtimes; the page lists the gates of
the release plan, the lines that did not change, and the three targets that were missed and accepted (`ReadFile` at
100K rows on the Intel VM, quoted `WriteString`, fixed-width `WriteString` at 10K rows on .NET 11 against .NET 10 on
the Intel VM). The comparison (AMD EPYC 9V74, 100K rows, .NET 10)
reads the 25-column file into objects in 169.9 ms against 196.3 ms for Sep (`SepComparison`) and in
168.4 ms against 523.1 ms for FileHelpers 3.5.2 (`DelimitedComparison`), and writes it in 33.74 ms against 123.8 ms for Sep; CsvHelper and Sep read quoted
fields faster, and Sylvan reads the 25-column file faster on the Intel VM (about 20 % on .NET 10, about 6 % on
.NET 11). Its raw files are under
[docs/benchmarks/raw/](docs/benchmarks/raw/README.md), and the Step 0 baseline of 4.9.1 is in
[docs/benchmarks/step0-4.10.md](docs/benchmarks/step0-4.10.md).

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
| `DelimitedParseBenchmarks` | Reading all rows, and only the first column, of the 25-column file | Sylvan.Data.Csv, CsvHelper | 10K, 100K, 1M (cut by `FH_BENCH_MAX_ROWS`) |
| `SepComparisonBenchmarks` | Reading all rows and writing them back to a string | Sep | 10K, 100K, 1M (cut by `FH_BENCH_MAX_ROWS`) |
| `QuotedFieldsBenchmarks` | Reading a file with quoted fields (embedded delimiters, doubled quotes); from 4.10 on the seeded random set, so its numbers are not comparable with the 4.8 and 4.9 quoted results | Sylvan.Data.Csv, CsvHelper, Sep | 10K, 100K |
| `QuotedFieldsPeriodicBenchmarks` | The same on the periodic quoted file (row i uses pattern i % 10); continues the 4.8 and 4.9 quoted series | Sylvan.Data.Csv, CsvHelper, Sep | 10K, 100K |
| `SparseQuoteBenchmarks` | `ReadFile` and `ReadFile_ParseAndDiscard` on the quoted layout where 1 % of the rows contain a quote | - | 10K, 100K |
| `LongLineBenchmarks` | The same on 8-column lines of 505 characters | - | 5K, 20K |
| `MultilineBenchmarks` | The same where every tenth row has a quoted value with a line break | - | 10K, 100K |
| `WideRecordBenchmarks` | The same on 150 columns (75 int, 75 string) | - | 1K, 10K |
| `QuotedWriteBenchmarks` | `WriteString` and `WriteStream_Null` (to `TextWriter.Null`) of the seeded quoted records | - | 10K, 100K |
| `FixedWidthWriteBenchmarks` | `WriteString` and `WriteStream_Null` of the fixed-width records | - | 10K, 100K |
| `WriteBenchmarks` | `WriteString` and `WriteFile` | CsvHelper | 10K, 100K, 1M (cut by `FH_BENCH_MAX_ROWS`) |
| `FixedWidthParseBenchmarks` | Fixed-width reads, class versus struct records | CsvHelper | 10K, 100K |
| `OptimizedParseBenchmarks` | `ReadFile`, `ReadString`, `ReadSpan` and their callback and async variants | - | 10K, 100K |
| `AsyncStreamingBenchmarks` | `MultiRecordAsyncEngine.ReadAsync` versus `MultiRecordEngine.ReadFile`, with and without cancellation | - | 10K, 100K |
| `EngineAsyncBenchmarks` | `FileHelperEngine<T>.ReadAsync(path)` and `ReadFileAsync(path, callback)` on the 25-column file, and `ReadAsync(path)` with a conversion error in every 100th row (`ErrorMode.SaveAndContinue`), `ReadAsync(Stream)` and `ReadAsync(TextReader)` of the file held in memory, `WriteStreamAsync` of the records to `Stream.Null`, and `WriteStreamAsync` with a source that throws after 3 records (`Rows / 100` calls per invocation); the last four use 4.10-only API; for the runtime-async A/B | - | 10K, 100K |
| `R6UpperBoundBenchmarks` | Gate R6: quoted parse-and-discard with the generated descriptor and with a descriptor that takes precomputed field ends (see below) | - | 10K, 100K |
| `G2PrototypeBenchmarks` | Gate G2: `WriteString` of the 25-column records with the generated `Format` and with a single-span `Format` (see below) | - | 100K |
| Component classes (namespace `...Benchmarks.Components`) | Tokenizers, the scan loop shape, converter call shapes, unquoting, string creation and one-record `Format` (see below) | - | fixed per class |

The write benchmarks set `NewLineForWrite = "\r\n"` and strip the line ending from the captured `HeaderText` (4.10 keeps it, 4.9.1 does not), so 4.9.1 and 4.10 write identical bytes. `SepComparisonBenchmarks` still compares that output with Sep output written with Sep's own line-ending setting, as in every earlier result.

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
`QuotedFields` also has `FileHelpers_ParseAndDiscard_ScanOnly` (T21): the same read, with the AppContext switch
`ModernLibs.FileHelpers.DisableTokenizedParse` set in its setup before the first engine, so a 4.10 build reads
through the scan path without the tokenized parse (the setup checks that). The library reads its switches once per
process: run this method in a process of its own, as `ab.py` does. `DelimitedParse` has the same twin (R2G) for the
25-column record: with the switch set, a 4.10 build reads it through the 4.9-shaped path (field count, then the
generated `Parse`).

### Competitor libraries added in 4.10 (T11)

**Tier A**: FlameCsv 0.3.1 and RecordParser 2.3.0 have `ReadAllRows_<Library>` and
`<Library>_ParseAndDiscard` methods in `DelimitedParseBenchmarks`, `SepComparisonBenchmarks`,
`DelimitedComparisonBenchmarks`, `QuotedFieldsBenchmarks` (and so `QuotedFieldsPeriodicBenchmarks`), and, for
RecordParser only, in `FixedWidthParseBenchmarks` and `FixedWidthComparisonBenchmarks` (FlameCsv has no fixed-width
mode). Every method reads into the same row type as the other readers and passes the same `DataCheck`. The filters
`*FlameCsv*` and `*RecordParser*` select every method of a library (the read methods are `ReadAllRows_<Library>`, so a
filter like `*FlameCsv_*` misses them).

**RecordParser runs on one thread.** RecordParser 2.3.0 parses on the thread pool by default
(`ParallelismOptions.Enabled` is true, one worker per core, original order kept), while every other library here is
single-threaded. All RecordParser benchmarks get their options from `RecordParserOptions` with
`ParallelismOptions = new() { Enabled = false }`, so all libraries run on one thread. There is no parallel variant.
The setup of each RecordParser method checks that these options have parallel processing off; it parses nothing, so
the measured process runs no extra RecordParser code. The thread-level proof is separate:
`dotnet run -c Release -f net10.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --check-competitors`
parses 20,000 rows of the 25-column and the fixed-width file with the same options and a parser that records its
thread, prints the result and exits (0, or 1 when any row was parsed off the calling thread) before BenchmarkDotNet
starts. It never runs in a measurement run. Run it after every RecordParser version bump (with parallel processing on
it reports 24 threads).

**Prototype check.** `--check-prototypes` is the same kind of switch for the component and prototype benchmarks (T10,
below):
`dotnet run -c Release -f net10.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --check-prototypes`
generates small datasets in a temporary folder, runs the comparisons between code paths that a benchmark's setup must
not run (see "Datasets" for why), prints one line per check and exits (0, or 1 when any check fails, with the first
difference) before BenchmarkDotNet starts:

- `R6_PrecomputedEnds` and `R6_PrecomputedEnds_FastUnquote` each give the same records as `R6_Current`, field by
  field, and each prototype parses every row (10,000 rows of the seeded quoted set);
- `G2_SingleSpan` writes the same text as `G2_Current`, and the prototype formats every record (10,000 records of the
  25-column file, header included);
- the four `Tokenizer_*` methods give the same field ends on every line of the 25-column, 150-column and long-line
  sets, and agree on random lines with too few, the right number and too many fields;
- `Scan_SpanCreate` gives the same result as `Scan_LoadUnsafe` (and both as a scalar reference) on every line of
  those sets, the seeded quoted set and random lines of delimiters, quotes and text.

The R6 and G2 comparisons run on 10,000 rows, while the benchmarks run up to 100,000. Correctness at 100,000 rows is
covered by each benchmark's own setup, which checks every record or written line against the dataset.

**A Step 0 round runs both checks before it measures**, `--check-competitors` and `--check-prototypes`, on every
configuration it measures (`dotnet <folder>/ModernLibs.FileHelpers.Benchmarks.dll --check-prototypes` for a built
configuration), and stops when either exits non-zero. A prototype that gives a different result measures something
else, and its gate would be decided on the wrong number.

**Tier B**: one table each, one `<Library>_ReadAll` method per library, with 10K and 100K rows:
`TierBComparisonBenchmarks` (25-column delimited file: NReco.Csv 1.0.3, TinyCsvParser 3.0.12, Cursively 1.2.0,
LumenWorksCsvReader 4.0.0, FlatFiles 5.0.4) and `TierBFixedWidthBenchmarks` (FlatFiles 5.0.4, RecordParser 2.3.0).
There are no parse-and-discard twins for tier B and no ModernLibs.FileHelpers row; read them against the same file and
`Rows` in `DelimitedParseBenchmarks` and `FixedWidthParseBenchmarks`.

Differences between the libraries that the mapping cannot remove:

- FlameCsv reads the file as UTF-8 bytes (`CsvOptions<byte>`) through a source-generated binding, which is how it is
  meant to be used; the other readers decode the file to `char` first. Cursively also reads bytes, so the benchmark
  decodes each field into a `char` buffer and then converts it exactly as NReco and LumenWorks do.
- NReco.Csv, LumenWorks and Cursively only deliver text fields. The benchmark converts them with `int.Parse`,
  `long.Parse`, `bool.Parse` and `DateTime.Parse` (invariant culture) through one shared `switch`, which is the
  work the object mappers of the other libraries do internally.
- TinyCsvParser maps by column index and checks every row's result (`IsSuccess`); its converters use the current
  culture, which its setup sets to the invariant one. FlatFiles and RecordParser build their mappers in the setup.
- RecordParser is quote-aware by default (`ContainsQuotedFields` is true in 2.3.0); it is set explicitly, for the
  PackageAssets files too, because every other CSV reader is quote-aware.
- The fixed-width mappings keep the padding (no trim), like `FixedRecord`.

### Libraries not included

- **ChoETL 1.2.1.72**: its dependency `System.Data.SqlClient` 4.4.3 has known vulnerabilities (NU1902,
  GHSA-8g2p-5pqh-5jmc, and NU1903, GHSA-98g6-xh36-x2p7). The build restores with NuGet audit as errors, so the
  package cannot be referenced without suppressing the audit.
- **Sylvan.Data 0.2.17 (fixed width)**: the package has no fixed-width reader (it holds the `DataBinder`, schema and
  object-reader types, and Sylvan.Data.Csv reads delimited text only), so there is nothing to map fairly.
- **ServiceStack.Text**: not added, by decision.

### Component benchmarks and prototypes (T10)

These classes measure parts of the read and write path on their own (design B3 and 2.5) and the two prototypes whose
numbers decide gates R6 and G2 of the 4.10 design. Their method names are fixed; the Step 0 report uses them as they
stand. The filter `*Components*` selects the component classes, `*Prototypes*` the two gate classes.

The brief of T10 named the tokenizer, scan, format-shape, gate and async methods. The methods of the three B3 classes
for unquoting, string creation and one-record `Format` were named in T10, and the Step 0 report uses them verbatim
too: `Unquote_Current`, `Unquote_IndexOf`, `CreateString_New`, `CreateString_Cached`, `FormatRecord_Current` and
`FormatRecord_SingleSpan`.

| Class | Methods (baseline first) | What it measures |
|---|---|---|
| `TokenizerBenchmarks` | `Tokenizer_CountPlusIndexOf`, `Tokenizer_SinglePassScalar`, `Tokenizer_SinglePassVector`, `Tokenizer_SinglePassVector_AnyMatchFirst` | The field ends of every line of a plain dataset (`Dataset`: 10,000 lines of the 25-column file, 1,000 of the 150-column file, 5,000 long lines) written into one preallocated `int[]`. The baseline is today's work: one `Count(',')` for the field-count check and N - 1 sequential `IndexOf(',')` in the generated parser. The vector tokenizers compare 16 (`Vector256`) or 8 (`Vector128`) characters at a time and walk the bits; `_AnyMatchFirst` tests the compare for a match before extracting the bits (expensive on Arm64). The 4.10 builds also have the shipped `RecordTokenizer` (T15, T21): `Tokenizer_Runtime` as the runtime calls it (the default loop), and `Tokenizer_Runtime_IndexOfHop`, `_Vector128`, `_Vector256`, `_Vector512`, `_Vector256_Packed` (two `Vector256` loads narrowed to their low bytes as a pre-test, design 2.5 item 16) and `_Vector256_LoadUnsafe` (against `_Vector256`, the span load), each one loop and load form of the same code. A method that needs `Vector256` or `Vector512` is not listed on a machine that does not accelerate it (`RequiresVectorAttribute`, `BenchConfig`). |
| `QuotedTokenizerBenchmarks` | `TokenizerQuoted_IndexOfHop`, `TokenizerQuoted_Vector128`, `TokenizerQuoted_Vector256` | 4.10 builds only (T21). The shipped quoted tokenizer (`RecordTokenizer.TokenizeQuoted`, T20) on 10,000 lines of the seeded quoted set, with one walk loop each: one `IndexOfAny` per delimiter or quote, `Vector128` blocks, `Vector256` blocks. |
| `VectorLoopShapeBenchmarks` | `Scan_LoadUnsafe`, `Scan_SpanCreate` | The delimiter-and-quote loop of `QuoteBalanceScanner.ScanSingleQuote`, reduced to counting, written with `Vector256.LoadUnsafe` from a `ref` (as in the library) and with `Vector256.Create(span.Slice(i, n))` (`Dataset`: the seeded quoted set, long lines). |
| `ConverterCallShapeBenchmarks` | `Format_GenericHelper`, `Format_GenericWriter`, `Format_DirectCall`, `Format_WriterDirectCall` | One `int`, one `DateTime` and one `string` field written 1,000 times into the engine's record buffer: through today's generic helper into a `stackalloc` buffer and a copy, through a generic `Format<TConverter, TValue>` on a `ref struct` writer, through direct `TryFormat` calls into one span, and (`Format_WriterDirectCall`, T58) through direct `TryFormat` calls on the `ref struct` writer. |
| `UnquoteBenchmarks` | `Unquote_Current`, `Unquote_IndexOf` | The quoted values of 10,000 lines of the seeded quoted set unquoted to a string: today's emitted strip pass (scalar search, rented buffer) against an `IndexOf` and `string.Create` helper (R3). `Values`: all quoted values, or only those with a doubled quote. |
| `CreateStringBenchmarks` | `CreateString_New`, `CreateString_Cached` | `new string(span)` against a helper that returns `string.Empty` and cached one-character strings (R4), on the string columns of the 25-column file and the Name and Category columns of the quoted set. |
| `FormatOneRecordBenchmarks` | `FormatRecord_Current`, `FormatRecord_SingleSpan` | `Format` of 1,000 PackageAssets records into the engine's record buffer, with the generated descriptor and with the G2 prototype: `Format` without the rest of `WriteString`. |
| `R6UpperBoundBenchmarks` | `R6_Current`, `R6_PrecomputedEnds`, `R6_PrecomputedEnds_FastUnquote` | Gate R6. `ReadFile(path, callback)` of the seeded quoted set (the work of `QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard`) with the generated descriptor, and in the same engine with hand-written descriptors whose `Parse` takes the field ends of each row from an array computed in the setup. They only slice, unquote, convert and construct, so they pay nothing for tokenizing. `R6_PrecomputedEnds` unquotes with today's emitted strip pass (doubled-quote search, rented buffer): R6 alone. `R6_PrecomputedEnds_FastUnquote` unquotes with the fastest correct code: R6 plus R3. **The gate uses `R6_PrecomputedEnds`**; its ratio is `R6_PrecomputedEnds / R6_Current` within one 4.10 build. The Step 0 report gives both prototype ratios. |
| `G2PrototypeBenchmarks` | `G2_Current`, `G2_SingleSpan` | Gate G2. `WriteString` of 100,000 records with the generated `Format`, and in the same engine with a hand-written `Format` that takes one `GetSpan(4096)`, writes the 25 fields with direct `TryFormat` calls and calls `Advance` once: the most that G2 can give. The record is still copied from the engine's record buffer into the result, so the gate does not include P5 (formatting straight into the result's chunk). |

**How the prototypes run.** `FileHelperEngine<T>` takes its descriptor from the registry, which holds the generated
one. The prototypes are run through `PrototypeEngine`, which builds a normal engine and replaces its descriptor and its
read core by reflection, so the prototype runs inside exactly the engine code the generated descriptor runs in: the line
reader, the multiline assembler and its quote scan, the field-count check, the `TextWriter` pipeline. Only the
descriptor's `Parse` (R6) or `Format` (G2) differs. If the engine's private fields change, `PrototypeEngine` throws
instead of measuring the generated descriptor. The prototypes need reflection over private fields, so the two gate
classes are not part of an AOT run.

`EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null` writes the 25-column dataset's records (read once in its own
setup) with `FileHelperEngine<T>.WriteStreamAsync` to `Stream.Null`; the setup checks one call's output length on a
`MemoryStream` and runs no other benchmark path. `Engine_WriteStreamAsync_Null_SourceThrows` is its exception-path
twin: `Rows / 100` calls per invocation, each over a source that yields 3 records and then throws, each exception
caught (the write path has no per-record error mode). Its setup checks one call's output (preamble and the 3 records),
that `TotalRecords` is 0 and that the invocation caught `Rows / 100`. `Engine_ReadAsync_Stream` and
`Engine_ReadAsync_TextReader` read the same file from memory with `ReadAsync(Stream)` and `ReadAsync(TextReader)`.
These four methods use 4.10-only API: they exist only in the 4.10
configurations, with no 4.9.x baseline.

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

**Row cap.** `DelimitedParseBenchmarks`, `SepComparisonBenchmarks` and `WriteBenchmarks` take their `Rows` values from
`BenchRows.Values` (`[ParamsSource]`): 10,000, 100,000 and 1,000,000, cut to the values at or below the number in
the environment variable `FH_BENCH_MAX_ROWS` when it is set. `round.sh --max-rows N` sets it for every measured
process (design D10: no 1,000,000 rows in A/B runs). `dotnet <dll> --check-rows` prints the values each of the three
classes will run, one line per class, and measures nothing.

**Local numbers are smoke checks (design D1).** A run on a developer machine or under WSL answers
"does it run", "is the data right" and "is the direction plausible". Its numbers are not evidence and
go into no report, gate or changelog; results come from Azure rounds only.

### 4.9.1 baselines and a whole round (T12)

The benchmark project can measure another library than this repository's, with the same benchmark code:

| Property | Library measured |
|---|---|
| (none) | this repository's library and generator |
| `-p:FileHelpersPackageVersion=4.9.1` | the published package (its `lib/net10.0` and the generator it bundles) |
| `-p:FileHelpersSourceRoot=<dir>` | the library and generator of another source tree, compiled with this repository's SDK |

A build with either property defines `FILEHELPERS_BASELINE`, which leaves out the benchmarks of 4.10 APIs that 4.9.1
does not have (the four `EngineAsyncBenchmarks` methods named above). The Step 0 filter runs them only between 4.10
builds; a filter line that names a pair with a 4.9.1 build gets a "not run" entry for them.

`benchmarks/tools/build-baselines.sh <out-dir>` builds the 4.9.1 baselines next to the folders of
`build-variants.sh`: `pkg-net10` and `pkg-net11` (the package, run on .NET 10 and .NET 11; the script checks that the
folder holds the package's own DLL) and `control-net10`, `control-net11-ra-on`, `control-net11-ra-off` (the `v4.9.1`
tag with `control-v4.9.1.patch`, which names SDK 11 in `global.json` and lets the library target `net11.0` with the
runtime-async switch). Each folder gets a `variant.txt`; `ab.py` needs `--expect-a`/`--expect-b` for these names.

`benchmarks/tools/round.sh <round-name> <commit> <filter-file>` runs a whole round on one machine: it records the
machine (the native toolchain included), clones this repository at the commit, builds all eight folders and
publishes the five folders of the Native AOT harness (`build-aot-variants.sh`, see "Native AOT timing harness"
below) one after the other, checks each one (`verify-variant`, the start-up lines, distinct library DLLs,
`--check-competitors`, `--check-prototypes`; for the AOT folders their `variant.txt` and `--info`) and stops with
`VARIANT MISMATCH <dir>` or `CHECK FAILED <check> <dir>` before anything is measured. Before it writes anything it
checks that the commit and the tag exist, that the running `round.sh` and `round_runs.py` are the commit's own copies,
and that the filter file is valid. Then `round_runs.py` runs `ab.py` with 7 launches for every A/B pair and line of the
filter file, the gate lines (group `prototype`) first. A failed `ab.py` run is written to `failed.txt` and the round
goes on; the last line is `ROUND COMPLETE <n> result files, <m> FAILED` (exit code 1 when m > 0), and the same command
with `--resume` keeps the finished result files and reruns the rest. `--resume` refuses another commit, another mode
(launches, job, iteration cap, `--dry`) or another filter file than the first call's, and runs again every result file
that was made for another glob, by other folders or from other binaries (SHA-256). `benchmarks/tools/filters-gates.txt`
holds only the two gate lines, for an uncapped rerun of the gates after a capped round. Filter lines of group `aot` run the four AOT pairs
with `ab.py --aot`. The filter file names the pairs each glob runs in (`benchmarks/tools/filters-step0.txt` describes
the format and the pairs). `--max-iterations N` passes `--maxIterationCount N` to every BenchmarkDotNet launch (N above
15, BenchmarkDotNet's `MinIterationCount`); `--job Dry` is refused without `--dry`. `--dry` is the local smoke run
(`--smoke`, 5 launches, job Dry): `round.sh dry-local HEAD benchmarks/tools/filters-dry.txt --dry`. `benchmarks/tools/round-report.py <root>/raw/<round-name>` prints the report
tables; [docs/benchmarks/step0-4.10.md](docs/benchmarks/step0-4.10.md) is the Step 0 report and
[docs/benchmarks/raw/README.md](docs/benchmarks/raw/README.md) describes the raw files.

### Round options and filter files (T59)

`round.sh` has four more options for the part 2 rounds. Every one is recorded in `machine.txt` (the lines `max-rows=`,
`base-commit=`, `env=`, `compare=`) and in its `mode:` line, which `--resume` compares, so a resumed round keeps the
first call's values and a call with other ones is refused (`error: --resume: measured as ...`).

| Option | Effect |
|---|---|
| `--max-rows N` | `FH_BENCH_MAX_ROWS=N` in the environment of every measured process (see "Row cap"). |
| `--base-commit SHA` | Also builds the library of commit `SHA` (a clone of this repository, no patch) into `base-net10` and `base-net11-ra-on` (`build-baselines.sh --base-commit SHA`) and enables the pairs `base-net10` (`base-net10 -> net10`) and `base-net11` (`base-net11-ra-on -> net11-ra-on`). A filter file that names them is refused without it, before anything is built. The benchmark project is this checkout's and defines `FILEHELPERS_BASELINE` for the base folders, so a method of an API that `SHA` lacks is "not run" in those pairs. `SHA` must differ from the measured commit. In a filter file, `all` in the pairs column does not include the two base pairs. The report has the section "Against the base commit". |
| `--env NAME=VALUE` | Repeatable. Sets the variable in the environment of every measured process (not in the checks that run before the measurements). Used as `--vm fh-amd-noavx2 --env DOTNET_EnableAVX2=0`. |
| `--compare` | The one-off library comparison, not an A/B (T50). For each of `net10`, `net11-ra-on` and `net11-ra-off`, the benchmark program runs from that folder with the filter globs, 3 launches (`--launches` is refused), BenchmarkDotNet's default job capped by `--max-iterations`. In `net11-ra-off` every method runs except the competitors' (`COMPARE_COMPETITORS` in `round_runs.py`: a method with a part such as `Sep`, `Sylvan` or `CsvHelper` in its name), because the other libraries do not change with the runtime-async switch. Only these three folders are built and checked (no package, control, base or AOT folder). The pairs column of the filter file is ignored (`filters-release.txt` writes `all`), no `ab.py` pair starts. Results go to `raw/<round>/<vm>/compare/<configuration>/`; `round-report.py --compare raw/<round>` builds one table per class with the columns `net10`, `net11-ra-on`, `net11-ra-off` (median of the launch medians and the range) and no verdict column; the mode cell says "comparison, 3 launches, not B4". |

Filter files of part 2 (format of `filters-step0.txt`): `filters-g1.txt` is the T19 round (run with `--max-rows 100000
--max-iterations 20`); it holds the write benchmarks the P7 and P9 checks need (`MultiRecordWriteBenchmarks.WriteStream_Null`,
`QuotedWriteBenchmarks.WriteStream_Null`, `QuotedWriteBenchmarks.WriteString`) and `ConverterCallShapeBenchmarks`
(`Format_WriterDirectCall` among its methods). `filters-simd.txt`, `filters-simd-noavx2.txt` (T21), `filters-write.txt`
(T25) and `filters-release.txt` (T50) have a header only; the task fills them, and `round.sh` refuses a filter file
without a line. `benchmarks/tools/dis.sh` runs one benchmark method under `DOTNET_JitDisasm` and writes the listing
(T19 runs it on the VMs).

## Native AOT timing harness

BenchmarkDotNet runs the benchmarks in its own process with the JIT (see "Running"), so it cannot measure a
Native AOT build. `benchmarks/ModernLibs.FileHelpers.Benchmarks.Aot` is a small console program, published with
`PublishAot=true`, that times one scenario under Native AOT (design 3.1, row C2): the 25-column parse-and-discard
read. It has no BenchmarkDotNet and uses no reflection.

**Scenarios.** `--scenario` picks one (`ab.py` passes it to every timed launch): `PackageAssets_ParseAndDiscard`
(the default, described below), `PackageAssets_ParseAndDiscard_NoVector` (T21: the same read with the AppContext
switch `ModernLibs.FileHelpers.DisableVectorizedTokenizer` set before the first engine; a 4.9.1 package has no such
switch) and `PackageAssets_WriteString` (T59), the work of
`WriteBenchmarks.WriteString_FileHelpers`: `WriteString` of the records the library read from the same file (CRLF,
header text without its line ending, as the benchmark prepares them). All scenarios share the data check and its
checksum, so `ab.py` takes one checksum from the `--checksum-only` runs and passes it to every timed launch of any
scenario. The write scenario also writes its records once, parses that output with the BCL parsers and refuses to time
(exit 3) unless it has the checksum of the data check; every run must return a string of the same length.

**What it measures.** The work of `DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard`: the same record type
(`Benchmarks/PackageAsset.cs`, compiled into both programs), the same dataset generator (`Data/DatasetGenerator.cs`,
also shared, so the file for a row count is byte-identical) and the same call: one `FileHelperEngine<PackageAsset>`
per process, and per iteration `ReadFile(path, r => checksum += r.Id)`. One process:

1. prints its start-up lines (variant, library, build, runtime);
2. generates the PackageAssets file of `--rows` rows (default 100,000) in `Data/` next to the executable, unless it
   is there already;
3. data check, not timed: a checksum of every field of every row (64-bit FNV-1a over the parsed values and the
   strings), once computed from the text with the BCL parsers and once from the records the library reads. They
   must agree, and with `--expect-checksum` they must also equal the checksum the other build of the pair read.
   Otherwise the process prints `// REFUSED, nothing timed: ...` and exits 3;
4. `--warmup` untimed reads (default 5), then `--iterations` timed reads (default 20). Before every timed read it
   runs a full garbage collection, outside the timed region; each read is timed with `Stopwatch.GetTimestamp` and
   must return the Id sum of the file. This differs from BenchmarkDotNet, which forces a collection between
   iterations, and one of its iterations can hold several reads (see "What it cannot do");
5. prints one JSON line: scenario, variant, library, build (`native-aot=yes`, app target framework, runtime
   identifier), runtime, rows, warm-up and iteration counts, the data checksum and every iteration time in ns.

```text
// ModernLibs.FileHelpers variant: tfm=net10.0 runtime-async=off
// ModernLibs.FileHelpers library: package 4.9.1
// Build: native-aot=yes app-tfm=net10.0 rid=linux-x64
// Runtime: .NET 10.0.15
{"harness":"aot","scenario":"PackageAssets_ParseAndDiscard","variant":"tfm=net10.0 runtime-async=off",...,"rows":100000,"warmup":5,"iterations":20,"checksum":"0x...","times_ns":[...]}
```

Exit codes: 0 done, 2 unusable arguments, 3 refused (nothing was timed). `--checksum-only` stops after the data
check and prints the JSON line without times; `--info` prints the start-up lines only.

**Build.** `benchmarks/tools/build-aot-variants.sh <out-dir> [<variant>...]` publishes the harness with `dotnet
publish -c Release -r <rid>` (the runtime identifier of the machine: `linux-x64` or `linux-arm64`), one
publish at a time, each into a clean folder with its own intermediate folder:

| Folder | Library compiled in | Harness |
|---|---|---|
| `net10` | this repository, `net10.0` | `net10.0` on .NET 10 |
| `net11-ra-on` | this repository, `net11.0`, runtime async on | `net11.0` on .NET 11 |
| `net11-ra-off` | this repository, `net11.0`, `-p:FileHelpersRuntimeAsync=false` | `net11.0` on .NET 11 |
| `pkg-net10` | the 4.9.1 package (`-p:FileHelpersPackageVersion=4.9.1`), its `lib/net10.0` | `net10.0` on .NET 10 |
| `pkg-net11` | the same package DLL | `net11.0` on .NET 11 |

The package build also takes the generator from the package, as a consumer gets it (`--package-version` picks
another version). After each publish the script stops unless the log has no `ILxxxx` diagnostic, the folder holds
the native executable and no `ModernLibs.FileHelpers.dll`, `verify-variant` run on the `ModernLibs.FileHelpers.dll`
the AOT compiler read (the `-r:` line of its `.ilc.rsp` file) prints the configuration of the folder's name, a
package folder's library is the package's own DLL (SHA-256 against the NuGet cache), and the executable started with
`--info` prints the same variant, `native-aot=yes` and the .NET major version of its target framework. Each folder
gets a `variant.txt` (the checked line, commit, SDK, library, its SHA-256, runtime identifier). The AOT compiler
needs a native toolchain on the machine: CI installs `clang` and `zlib1g-dev` when `clang` is missing (job
`aot-smoke`); without `clang` the SDK falls back to `gcc`, which is how it publishes under WSL here.

**Run.** `ab.py --aot` runs the folders interleaved, with the B4 rule, the 7-launch minimum, `--smoke` and
`--allow-dirty` exactly as for BenchmarkDotNet folders. Arguments after `--` go to the harness (`--rows`,
`--warmup`, `--iterations`; `--expect-checksum`, `--checksum-only`, `--info`, `--data-dir` and `--scenario` are refused,
ab.py sets or forbids them; the `--filter` glob selects the scenarios). Before the first timed launch, `ab.py` runs each side once with `--checksum-only` and stops
unless both report the same data checksum; every timed launch then gets `--expect-checksum` with that value. One
sample is the median of the iteration times of one launch. The start-up lines of every process must show the
folder's variant and `native-aot=yes`; for `pkg-net10` and `pkg-net11` also a package library and the .NET major
version of the folder's name (both package folders print the same variant line, so the runtime is what tells them
apart), for `net10`, `net11-ra-on` and `net11-ra-off` the `project` library. `ab.py` prints these lines for each
side after the checksum run. A and B must hold different executables (the same folder twice, or a copy, is
refused), and the SHA-256 of each executable must not change during the run. With `--aot`, `pkg-net10` and
`pkg-net11` need no `--expect-a`/`--expect-b`. The
result file has `"harness": "aot"`, `"aot_checksum"` and `"program_args"`; the case is named
`ModernLibs.FileHelpers.Benchmarks.Aot.PackageAssets_ParseAndDiscard(Rows: 100000)`.

```bash
benchmarks/tools/build-aot-variants.sh ~/fh-aot
python3 -I benchmarks/tools/ab.py --aot --a ~/fh-aot/pkg-net10 --b ~/fh-aot/net10 \
  --expect-a 'tfm=net10.0 runtime-async=off' --launches 7 --out aot-net10.json -- --rows 100000
```

**What it cannot do.**

- Its numbers compare AOT builds with each other (4.9.1 against 4.10, runtime async on against off). They are not a
  ratio against a BenchmarkDotNet number: the timing loops differ (BenchmarkDotNet has a pilot stage, chooses its
  iteration counts, removes outliers and subtracts its overhead; this harness has fixed counts and none of that).
- Garbage collection: the harness collects before every read, so no read pays for the garbage of the one before.
  BenchmarkDotNet collects between iterations only, and at 10K and 100K rows one iteration can hold several reads,
  so later reads of an iteration can pay for a collection. Expect AOT times per read to be somewhat lower for that
  reason alone.
- No AOT control build. For the JIT pairs, the control build (the 4.9.1 source compiled with SDK 11, design 2.5
  item 9) separates the compiler and SDK effect from the code effect. The AOT pairs compare the 4.9.1 package with
  this build only, so an AOT 4.9.1-against-4.10 ratio contains both the code change and the compiler and SDK
  change. Read it as "what a consumer gets", not as "what our code changed".
- Instruction set: the publish sets no `IlcInstructionSet` (the `.ilc.rsp` has no `--instruction-set` line), so the
  AOT compiler targets its default x64 baseline. That this baseline does not assume AVX2, so that code the JIT compiles
  for AVX2 on the same machine takes a narrower vector path under AOT, is [unverified]: the rounds of 4.10 do not record
  it (each round's `machine.txt` names the native toolchain, clang 18.1.3, and `variants.txt` the AOT folders, but
  neither prints the instruction set or the loop the tokenizer chose). What the scenarios showed (round
  `simd-2026-11-17`, docs/benchmarks/v4.10.0.md, "Native AOT"): under AOT the default read is slower than the read with
  `DisableVectorizedTokenizer` set on .NET 10 (1.172 on `fh-amd`, 1.090 on `fh-intel`) and faster on .NET 11 (0.889 and
  0.932), while under the JIT the vector loops are faster than `IndexOfHop` on both runtimes. An AVX2 AOT build would
  need `-p:IlcInstructionSet=x86-64-v3`, which the script does not pass.
- No memory diagnoser: no Gen0/Gen1/Gen2 or allocation columns.
- One scenario. Another one needs code in the harness and an entry in `AOT_SCENARIOS` of `ab.py`.
- The published program has the feature switches `PublishAot` sets for every AOT application (for example
  `EventSource` support off); that is the AOT configuration a consumer gets. Globalization is not invariant, as in
  the BenchmarkDotNet process, so ICU must be installed.
- The 4.9.1 package ships `lib/net10.0` only, so both package folders compile the same `net10.0` library, and
  runtime async does not apply to it. The read is synchronous, so `net11-ra-off` against `net11-ra-on` is expected
  to show no difference (an A/A check under AOT).
- Local numbers are smoke checks (D1), as for every other run.

The Step 0 round runs four AOT pairs on every VM, each with 7 launches of 100,000 rows (`<bin>` is the round's build
folder, `<raw>` its result folder):

```bash
benchmarks/tools/build-aot-variants.sh <bin>/aot
python3 -I benchmarks/tools/ab.py --aot --a <bin>/aot/pkg-net10 --b <bin>/aot/net10 --expect-a 'tfm=net10.0 runtime-async=off' --launches 7 --out <raw>/aot/v410-net10.json -- --rows 100000 --warmup 5 --iterations 20
python3 -I benchmarks/tools/ab.py --aot --a <bin>/aot/pkg-net11 --b <bin>/aot/net11-ra-on --expect-a 'tfm=net10.0 runtime-async=off' --launches 7 --out <raw>/aot/v410-net11.json -- --rows 100000 --warmup 5 --iterations 20
python3 -I benchmarks/tools/ab.py --aot --a <bin>/aot/pkg-net11 --b <bin>/aot/net11-ra-off --expect-a 'tfm=net10.0 runtime-async=off' --launches 7 --out <raw>/aot/v410-net11-ra-off.json -- --rows 100000 --warmup 5 --iterations 20
python3 -I benchmarks/tools/ab.py --aot --a <bin>/aot/net11-ra-off --b <bin>/aot/net11-ra-on --launches 7 --out <raw>/aot/ra.json -- --rows 100000 --warmup 5 --iterations 20
```

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
- **PackageAssets with bad rows** (`package_assets_bad100_v1_*`): the PackageAssets file with an `x` before the
  DownloadCount of every 100th data row, so each of those rows fails to convert (read by `EngineAsyncBenchmarks`)
- **Multi-record**: alternating `H|…` and `D|…` lines (generated by `AsyncStreamingBenchmarks`)
- **Floats**: numeric data (generator only; no benchmark reads it yet)

Each benchmark has its own `[GlobalSetup(Target = ...)]`. It runs that benchmark method once and
compares its result with the raw dataset (row count and sampled fields for reads, every line for
writes), and throws on a difference: a benchmark that parses its data wrongly fails instead of
reporting a number. The setup runs no other benchmark, because what another library or read leaves in
the process (for example pooled buffers) changes the garbage collector's timing and with it the
measured time. For the same reason, results come from one benchmark method per process (see
"Running"). A check that needs two code paths, such as a prototype against the current code, is therefore not
in any setup: it is in `--check-prototypes` (see "Prototype check" above), which a round runs before it measures.
