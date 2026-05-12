# ModernLibs.FileHelpers

Modern, AOT-ready rewrite of [FileHelpers](https://github.com/MarcosMeli/FileHelpers) for .NET 10. High-performance delimited and fixed-width file parsing with zero per-record allocations for `record struct` types.

**API compatible with FileHelpers 3.x** — migration is a single `using` change.

## Features

- **Delimited records** — CSV, TSV, pipe-delimited, custom delimiters
- **Fixed-length records** — positional field parsing with `SearchValues<char>` SIMD acceleration
- **Multi-record files** — closed set of record types with selector dispatch
- **Master-detail files** — grouped records with classification callbacks
- **Async streaming** — `IAsyncEnumerable<T>` over `System.IO.Pipelines` with backpressure
- **Source generator** — `[DelimitedRecord]` and `[FixedLengthRecord]` attributes generate descriptors at compile time
- **AOT compatible** — no reflection, no `Expression<>`, no `DynamicMethod`
- **Zero allocations** — `record struct` types produce zero per-record heap allocations

## Quick Start

```bash
dotnet add package ModernLibs.FileHelpers
```

```csharp
using ModernLibs.FileHelpers;  // was: using FileHelpers;

[DelimitedRecord(",")]
public record struct Customer(
    [property: FieldOrder(1)] int Id,
    [property: FieldOrder(2)] string Name,
    [property: FieldOrder(3)] decimal Balance
);

var engine = new FileHelperEngine<Customer>();
Customer[] customers = engine.ReadFile("customers.csv");
```

## Performance

Built for speed with:
- `SearchValues<char>` for SIMD-accelerated delimiter detection
- `ArrayPool<T>.Shared` for buffer reuse
- `Span<char>` + `string.Create()` — no `StringBuilder` in hot paths
- `ReadOnlySpan<char>` index slicing — no `string.Split()`

Benchmarks compare against Sep and Sylvan. See `benchmarks/` for details.

## Migration from FileHelpers 3.x

Change your `using` statement and you're done:

```diff
- using FileHelpers;
+ using ModernLibs.FileHelpers;
```

All existing attributes, engines, and converters work the same way.

## Documentation

- [Opt-In Performance Features](docs/OPT-IN-FEATURES.md) — streaming, recycling, pooling, and ref struct records
- [Packaging Guide](docs/PACKAGING.md) — versioning, pre-release policy, release checklist
- [Samples](samples/) — migration examples from FileHelpers 3.x
- [Benchmarks](benchmarks/) — BenchmarkDotNet performance tests

## License

MIT — see [LICENSE](LICENSE).
