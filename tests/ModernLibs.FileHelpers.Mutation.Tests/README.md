# ModernLibs.FileHelpers.Mutation.Tests

Mutation-test runner shell for the ModernLibs.FileHelpers specialist surface. The project hosts xUnit-runner-compatible cases that Stryker.NET mutates, so it is intentionally separate from the primary TUnit suite at `tests/ModernLibs.FileHelpers.Tests`.

## Status (v1.3 mutation suite — Phase 30)

This project contains **78** xUnit `[Fact]` mutation-target tests covering the core parsing and engine surface:

| Target | Test Count | Coverage Areas |
|--------|------------|----------------|
| `DelimitedFieldEnumerator` | 25 | Delimiter detection, quoted fields, escape sequences, per-field delimiters, lenient behavior |
| `FixedLengthSlicer` | 10 | Offset/width slicing, boundary conditions, overflow clamping |
| `SpanLineReader` | 14 | LF/CRLF/CR handling, line numbering, large buffers, empty input |
| `MultiRecordEngine` | 15 | Selector dispatch, error modes (Throw/Ignore/Save), multi-type parsing, I/O parity, write path |
| `MasterDetailEngine` | 14 | Classification callback, multi-line buffering, error modes, I/O parity, write path, grouping |

## Reproducible commands

### Run the mutation test suite

```bash
dotnet test --project tests/ModernLibs.FileHelpers.Mutation.Tests/
```

### Run Stryker.NET mutation analysis

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --config-file stryker-config.json
```

Configuration is in `stryker-config.json` at the repo root with:
- Target: `src/ModernLibs.FileHelpers/ModernLibs.FileHelpers.csproj`
- Thresholds: high=80%, low=60%, break=50%
- Baseline enabled with fallback to `main`
- Reporters: progress, dots, html

### Run via the testing platform

```bash
./tests/ModernLibs.FileHelpers.Mutation.Tests/bin/Release/net10.0/ModernLibs.FileHelpers.Mutation.Tests
```

## CI Integration

Mutation testing runs in CI after the unit test suite passes. The workflow:
1. Installs `dotnet-stryker` global tool
2. Runs `dotnet stryker --config-file stryker-config.json`
3. Uploads the mutation report as a build artifact (30-day retention)

See `.github/workflows/ci.yml` for the full pipeline.
