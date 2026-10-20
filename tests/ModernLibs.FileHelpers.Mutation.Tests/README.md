# ModernLibs.FileHelpers.Mutation.Tests

xUnit v3 test project used as the Stryker.NET mutation-testing target. It is separate from the primary TUnit
suite at `tests/ModernLibs.FileHelpers.Tests` because Stryker.NET does not support Microsoft.Testing.Platform
projects (the TUnit suite); this project uses the xUnit console runner instead.

## Contents

81 xUnit tests:

| Target | Tests | Coverage areas |
|--------|-------|----------------|
| `FixedLengthSlicer` | 22 | Offset/width slicing, boundary conditions, overflow clamping |
| `SpanLineReader` | 29 | LF/CRLF/CR handling, line numbering, large buffers, empty input |
| `MultiRecordEngine` | 15 | Selector dispatch, error modes (Throw/Ignore/Save), multi-type parsing, I/O parity, write path |
| `MasterDetailEngine` | 15 | Classification callback, multi-line buffering, error modes, I/O parity, write path, grouping |

Quote handling (`QuoteBalanceScanner`, the generated splitter), the read core and the converters are covered
by the TUnit suite, not by this project.

## Commands

Run the tests:

```bash
dotnet build -c Release tests/ModernLibs.FileHelpers.Mutation.Tests
./tests/ModernLibs.FileHelpers.Mutation.Tests/bin/Release/net10.0/ModernLibs.FileHelpers.Mutation.Tests
```

Run Stryker.NET:

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --config-file stryker-config.json
```

`stryker-config.json` (repository root) mutates the core parser files (read core, record session, logical
line assembler, line readers and splitter, quote scanner, fixed-length slicer, `NumberConversion`) and the
multi-record / master-detail engines. Thresholds: high 80 %, low 60 %, break 50 %.

## Known issue

With Stryker.NET 5.0 every mutant is reported as survived (score 0 %, "test coverage capture failed"),
even for `FixedLengthSlicer`, which these tests cover well. The CI step is therefore informational
(`continue-on-error`) until the runner integration is fixed.
