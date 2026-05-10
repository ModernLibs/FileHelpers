# Benchmarks

ModernLibs.FileHelpers performance benchmarks using BenchmarkDotNet.

## Quick Run

```bash
dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks
```

## Categories

| Category | What it measures |
|----------|-----------------|
| DelimitedParseBenchmarks | CSV/delimited read performance vs Sylvan, CsvHelper |
| FixedWidthParseBenchmarks | Fixed-width parse performance, class vs struct, SpanSlicer |
| WriteBenchmarks | Write throughput vs CsvHelper |
| AsyncStreamingBenchmarks | Async streaming performance, cancellation, allocation budget |

## Results

Full results: [docs/benchmarks/v4.0.0.md](docs/benchmarks/v4.0.0.md)

## Datasets

Benchmarks use deterministic (seeded) datasets generated on first run:
- **PackageAssets** — 1M rows × 25 columns (delimited)
- **Floats** — 100K rows of numeric data
- **Fixed-width** — 500K records, 80 chars each, 7 fields

Data files are stored in `benchmarks/.../Data/` and reused across runs.
