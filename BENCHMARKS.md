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

4.8 is about twice as fast as FileHelpers 3.5.2 on these reads and faster than CsvHelper everywhere
measured, writes as fast as Sep, but reads slower than Sep and Sylvan. `ReadFile(path)` to an array
allocates about 2.5 times more than Sep or Sylvan because it loads the whole file into a string; the
streaming `ReadFile(path, callback)` overload allocates the same as Sep. The results page lists the
caveats (WSL2, what each library does per row).

Performance depends heavily on hardware, record shape and field types, so run the suite on a machine
like your production one.

[docs/benchmarks/v4.0.0.md](docs/benchmarks/v4.0.0.md) holds an early development snapshot. It was
recorded before the 4.3.0 milestone and is kept for history only. Its numbers are not comparable:
before 4.8.0 the benchmark records parsed headers and dates incorrectly (conversion failures were
silent) and the "FileHelpers 3.x" records actually used this library.

When results are published, each file in `docs/benchmarks/` states the library version and commit,
the date, the hardware, OS, .NET SDK and runtime, and the BenchmarkDotNet version.

## Running

Always run in Release on an otherwise idle machine:

```bash
# everything (takes a long time)
dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'

# one class
dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*FixedWidthComparisonBenchmarks*'

# quicker, less precise
dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*SepComparison*' --job short

# as for the published results: default job, markdown tables
dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*SepComparison*' --memory --exporters github
```

BenchmarkDotNet writes reports to `BenchmarkDotNet.Artifacts/` in the current directory.

## What is measured

| Class | Measures | Compared with | Rows |
|---|---|---|---|
| `DelimitedComparisonBenchmarks` | Reading a 25-column delimited file into an array | FileHelpers 3.5.2 | 10K, 100K |
| `FixedWidthComparisonBenchmarks` | Reading a fixed-width file (class and struct records) | FileHelpers 3.5.2 | 10K, 100K |
| `DelimitedParseBenchmarks` | Reading all rows, and only the first column, of the 25-column file | Sylvan.Data.Csv, CsvHelper | 10K, 100K, 1M |
| `SepComparisonBenchmarks` | Reading all rows and writing them back to a string | Sep | 10K, 100K, 1M |
| `QuotedFieldsBenchmarks` | Reading a file with quoted fields (embedded delimiters, doubled quotes) | Sylvan.Data.Csv, CsvHelper, Sep | 10K, 100K |
| `WriteBenchmarks` | `WriteString` and `WriteFile` | CsvHelper | 10K, 100K, 1M |
| `FixedWidthParseBenchmarks` | Fixed-width reads, class versus struct records | - | 10K, 100K |
| `OptimizedParseBenchmarks` | `ReadFile`, `ReadString`, `ReadSpan` and their callback and async variants | - | 10K, 100K |
| `AsyncStreamingBenchmarks` | `MultiRecordAsyncEngine.ReadAsync` versus `MultiRecordEngine.ReadFile`, with and without cancellation | - | 10K, 100K |

Competitor versions are pinned in `Directory.Packages.props`: FileHelpers 3.5.2, Sep 0.14.1,
Sylvan.Data.Csv 1.4.0, CsvHelper 33.0.1, BenchmarkDotNet 0.15.8.

Note that the libraries do different amounts of work: ModernLibs.FileHelpers and FileHelpers 3.x
produce typed records with converters and validation, while the CSV readers are measured with the
row-to-object code written in each benchmark. Compare like with like.

## Datasets

The datasets are generated on the first run and reused afterwards. Each one is a pure function of its
row count (fixed seed, fixed base date), so every run and every machine reads the same bytes. They are
stored in a `Data` folder next to the compiled benchmark assembly (under `bin/`); the file names carry
a format version, bumped whenever the generated text changes.

- **PackageAssets**: delimited, 25 columns (up to 1M rows), a header line, ISO 8601 dates
  (`yyyy-MM-ddTHH:mm:ss`, read with `[FieldConverter(ConverterKind.Date, "s")]`) and empty text columns
- **Fixed-width**: 80-character records, 7 fields
- **Quoted fields**: delimited with a header line and RFC 4180 quoted values
- **Multi-record**: alternating `H|…` and `D|…` lines (generated by `AsyncStreamingBenchmarks`)
- **Floats**: numeric data (generator only; no benchmark reads it yet)

Every `[GlobalSetup]` runs each benchmark method once and compares its result with the raw dataset
(row count and sampled fields for reads, every line for writes), and throws on a difference. A
benchmark that parses its data wrongly fails instead of reporting a number.
