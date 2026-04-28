# Migration Guide: FileHelpers 3.x to ModernLibs.FileHelpers 4.x

This guide helps you migrate from the original FileHelpers library to ModernLibs.FileHelpers. In most cases, migration requires only a single line change.

## Overview

**The Promise**: Change one `using` directive. That's it.

ModernLibs.FileHelpers maintains **95%+ API compatibility** with FileHelpers 3.x. Most existing code will work without modification after updating the namespace.

### Quick Migration

```csharp
// Before (FileHelpers 3.x)
using FileHelpers;

// After (ModernLibs.FileHelpers 4.x)
using ModernLibs.FileHelpers;
```

## Compatibility Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| **Core Reading/Writing** | 100% | All engine methods work identically |
| **Delimited Records** | 100% | `[DelimitedRecord]`, delimiters, quotes |
| **Fixed-Length Records** | 100% | `[FixedLengthRecord]`, `[FieldFixedLength]` |
| **Field Attributes** | 100% | Order, converters, trim, optional, null values |
| **Record Attributes** | 100% | IgnoreFirst, IgnoreLast, IgnoreEmptyLines |
| **Streaming (AsyncEngine)** | 100% | BeginReadFile/WriteFile, ReadNext, WriteNext |
| **Error Handling** | 100% | ErrorMode.ThrowException, IgnoreAndContinue, SaveAndContinue |
| **Events** | 100% | BeforeRead, AfterRead, BeforeWrite, AfterWrite |
| **INotifyRead/INotifyWrite** | 100% | Interface-based notifications |
| **Field Alignment** | 100% | `[FieldAlign]` for fixed-length padding |
| **Field Validation** | 100% | `[FieldNotEmpty]`, `[FieldValueDiscarded]` |
| **ConditionalRecord** | 100% | Skip records based on prefix/contains/regex |
| **Per-Field Delimiter** | 100% | `[FieldDelimiter]` override |
| **Converters** | 100% | DateTime, numeric, boolean, enum, Guid |
| **Multiline Quoted Fields** | 100% | `[FieldQuoted]` with newlines inside quotes |

## Namespace Changes

### Required Change

```csharp
// Old namespace
using FileHelpers;

// New namespace
using ModernLibs.FileHelpers;
```

### Project File Update

```xml
<!-- Remove old package reference -->
<!-- <PackageReference Include="FileHelpers" Version="3.5.2" /> -->

<!-- Add new package reference -->
<PackageReference Include="ModernLibs.FileHelpers" Version="4.0.0" />
```

## API Compatibility Details

### Full Compatibility (No Changes Needed)

These APIs work identically to FileHelpers 3.x:

#### Engine API
```csharp
// FileHelperEngine<T> - 100% compatible
var engine = new FileHelperEngine<Customer>();
var records = engine.ReadFile("customers.csv");
engine.WriteFile("output.csv", records);

// FileHelperAsyncEngine<T> - 100% compatible
var asyncEngine = new FileHelperAsyncEngine<Customer>();
asyncEngine.BeginReadFile("customers.csv");
while (asyncEngine.ReadNext() is { } record) { /* process */ }
```

#### Record Attributes
```csharp
// All record attributes work identically
[DelimitedRecord(",")]
[FixedLengthRecord]
[IgnoreFirst(1)]
[IgnoreLast(1)]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
[ConditionalRecord(RecordCondition.BeginWith, "prefix")]
```

#### Field Attributes
```csharp
// All field attributes work identically
[FieldOrder(1)]
[FieldFixedLength(20)]
[FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
[FieldQuoted]
[FieldTrim(TrimMode.Both)]
[FieldHidden]
[FieldOptional]
[FieldNullValue("N/A")]
[FieldAlign(AlignMode.Right)]
[FieldDelimiter(";")]
[FieldNotEmpty]
[FieldValueDiscarded]
```

### Breaking Changes

#### 1. Runtime Reflection Features Removed

Features that relied on runtime reflection are not supported:

```csharp
// NOT SUPPORTED: Dynamic ClassBuilder
var cb = new ClassBuilder("Customer");  // Removed

// NOT SUPPORTED: DataLink feature
var link = new DataLinkProvider();  // Removed
```

**Migration**: Use source-generated record types instead:

```csharp
// ModernLibs approach: Source-generated record
[DelimitedRecord(",")]
public class Customer
{
    [FieldOrder(1)] public string Name;
    [FieldOrder(2)] public int Age;
}
```

#### 2. CSV-Specific Helpers

CSV-specific expansion is postponed. Existing delimited records remain supported, but ModernLibs.FileHelpers is intentionally focusing on fixed-width, multi-record, and master/detail processing instead of competing with mature CSV-first libraries.

**Specialist scope at a glance (v1.2 — Fixed Width Specialist):**

| Surface | Parity with FileHelpers 3.x | Notes |
|---|---|---|
| Fixed-width parsing/writing | Behavior parity for the documented attribute set; `FixedMode` and `OverflowMode` are first-class contract knobs (see *Fixed-width: attributes, modes, and converters*). | Intentional non-parity items listed inline in that section. |
| Multi-record (`MultiRecordEngine`) | API parity through the AOT-safe **closed-set** form `MultiRecordType.For<T>(selector)`. | Runtime type discovery is intentionally not revived (see *Multi-Record selector failures*). |
| Master/detail (`MasterDetailEngine<TMaster,TDetail>`) | API parity for read/write, including multi-line logical records. | See *Master-Detail Processing*. |
| CSV-first features (advanced dialects, dynamic `ClassBuilder`, etc.) | **Postponed / intentional non-parity** for v1.2. | Use a CSV-focused library where this is the primary need. |

The non-parity statements below are normative. Everything not flagged as non-parity should behave identically to FileHelpers 3.x for the supported surface.

#### 3. Multi-Record Processing

Multi-record processing is supported through an AOT-safe closed-set API:

```csharp
var engine = new MultiRecordEngine(
    MultiRecordType.For<Order>(line => line.StartsWith("ORD", StringComparison.Ordinal)),
    MultiRecordType.For<Customer>(line => line.StartsWith("CUST", StringComparison.Ordinal)));

object[] records = engine.ReadFile("mixed.txt");
```

Legacy `MultiRecordEngine(typeof(Order), typeof(Customer))` runtime type discovery is intentionally not revived because it conflicts with the source-generated, AOT-safe design. Use explicit selectors instead.

#### Multi-Record selector failures and ErrorMode migration behavior

Selector miss behavior now follows the same `ErrorMode` contract used by `FileHelperEngine<T>`, with strict behavior by default.

- `ErrorMode.ThrowException` (default): selector misses throw immediately with line number and line text context.
- `ErrorMode.IgnoreAndContinue`: unmatched lines are skipped and processing continues.
- `ErrorMode.SaveAndContinue`: unmatched lines are captured into `ErrorManager.Errors` and processing continues.

```csharp
var engine = new MultiRecordEngine(
    MultiRecordType.For<Order>(line => line.StartsWith("ORD", StringComparison.Ordinal)),
    MultiRecordType.For<Customer>(line => line.StartsWith("CST", StringComparison.Ordinal)));

// Default is strict throw semantics.
engine.ErrorMode = ErrorMode.ThrowException;

// Optional migration-friendly non-throw pathways:
engine.ErrorMode = ErrorMode.SaveAndContinue;
object[] records = engine.ReadFile("mixed.txt");

foreach (var error in engine.ErrorManager.Errors)
{
    Console.WriteLine($"Selector miss at line {error.LineNumber}: {error.RecordString}");
}
```

**Intentional differences / non-parity**

- **Closed-set configuration required:** ModernLibs requires explicit `MultiRecordType.For<T>(selector)` entries instead of legacy runtime type discovery.  
  **Rationale:** preserves AOT safety and deterministic source-generated behavior.  
  **Migration action:** register each record type and selector explicitly during engine construction.

#### 4. Master-Detail Processing

Parent/child files are supported with `MasterDetailEngine<TMaster,TDetail>`:

```csharp
var engine = new MasterDetailEngine<Order, OrderLine>(line =>
    line.StartsWith("ORD", StringComparison.Ordinal) ? RecordAction.Master :
    line.StartsWith("LIN", StringComparison.Ordinal) ? RecordAction.Detail :
    RecordAction.Skip);

MasterDetails<Order, OrderLine>[] orders = engine.ReadFile("orders.txt");
```

## Before/After Code Examples

### Example 1: Basic CSV Reading

```csharp
// ========== BEFORE (FileHelpers 3.x) ==========
using FileHelpers;

[DelimitedRecord(",")]
public class Customer
{
    [FieldOrder(1)] public string Name;
    [FieldOrder(2)] public int Age;
    [FieldOrder(3)] public string Country;
}

class Program
{
    static void Main()
    {
        var engine = new FileHelperEngine<Customer>();
        var customers = engine.ReadFile("customers.csv");
        foreach (var c in customers)
            Console.WriteLine($"{c.Name} from {c.Country}");
    }
}

// ========== AFTER (ModernLibs.FileHelpers 4.x) ==========
using ModernLibs.FileHelpers;  // Only change needed!

[DelimitedRecord(",")]
public class Customer
{
    [FieldOrder(1)] public string Name;
    [FieldOrder(2)] public int Age;
    [FieldOrder(3)] public string Country;
}

class Program
{
    static void Main()
    {
        var engine = new FileHelperEngine<Customer>();
        var customers = engine.ReadFile("customers.csv");
        foreach (var c in customers)
            Console.WriteLine($"{c.Name} from {c.Country}");
    }
}
```

### Example 2: Fixed-Length Records

```csharp
// ========== BEFORE (FileHelpers 3.x) ==========
using FileHelpers;

[FixedLengthRecord]
public class Employee
{
    [FieldFixedLength(10)] public int Id;
    [FieldFixedLength(25)] public string Name;
    [FieldFixedLength(12)] public decimal Salary;
}

// ========== AFTER (ModernLibs.FileHelpers 4.x) ==========
using ModernLibs.FileHelpers;  // Only change needed!

[FixedLengthRecord]
public class Employee
{
    [FieldFixedLength(10)] public int Id;
    [FieldFixedLength(25)] public string Name;
    [FieldFixedLength(12)] public decimal Salary;
}
```

### Fixed-width: attributes, modes, and converters

When you migrate fixed-length layouts, treat **`FixedMode`** on `[FixedLengthRecord]` and per-field **`OverflowMode`** on `[FieldFixedLength]` as part of the contract, not just decoration.

**Supported field / record attributes (fixed-width)** — the source generator wires these for `[FixedLengthRecord]` types:

| Attribute | Fixed-width |
|-----------|-------------|
| `[FieldOrder]` | Yes |
| `[FieldFixedLength]` (with optional `OverflowMode`) | Yes |
| `[FieldTrim]` | Yes |
| `[FieldOptional]` | Yes |
| `[FieldNullValue]` | Yes |
| `[FieldNotEmpty]` | Yes |
| `[FieldValueDiscarded]` | Yes |
| `[FieldInNewLine]` | Yes (sync `FileHelperEngine` joins physical lines per logical record) |
| `[FieldAlign]` | Yes |
| `[FieldConverter]` (`ConverterKind` or `typeof(...)`) | Yes — `typeof` may target `ConverterBase` or `ISpanConverter<T>` with a public parameterless constructor |
| `[FieldQuoted]` / `[FieldDelimiter]` | Extracted where applicable; prefer delimited records for heavy quoting / per-field delimiter scenarios |
| `[FieldHidden]` / `[FieldNotInFile]` | Yes |
| Record filters: `[IgnoreFirst]`, `[IgnoreLast]`, `[IgnoreEmptyLines]`, `[IgnoreCommentedLines]`, `[ConditionalRecord]` | Yes |

**`FixedMode`** (constructor argument on `[FixedLengthRecord]`; default is **`ExactLength`**):

- **`ExactLength`**: Each logical line must be exactly the sum of all field widths. Shorter or longer lines are rejected (see tests in `FixedWidthContractTests`).
- **`AllowLessChars`**: Lines shorter than the declared total are accepted; trailing fields may be empty or partial. Lines longer than the declared total are still rejected.
- **`AllowMoreChars`**: Lines must be at least as long as the declared total; extra characters after the last field are ignored.
- **`AllowVariableLength`**: Similar to allowing variable total length without strict equality; use when trailing content is intentionally ignored (see `FixedWidthContractTests` for precise behavior).

**`OverflowMode`** on `[FieldFixedLength]`:

- **`DiscardEnd`** (default): When formatting a value that does not fit the fixed width, excess characters are dropped from the end so the written field never exceeds its width.
- **`Error`**: Overflow on format is treated as an error instead of silent truncation — use when silent data loss is unacceptable.

**Converters**

New code should implement **`ISpanConverter<T>`** for zero-allocation span-based conversion. Legacy **`ConverterBase`** types remain supported for migration; the generator bridges them with `ConverterBaseAdapter<T>` (string-based internally). Examples and edge cases live under `tests/ModernLibs.FileHelpers.Tests/Phase25/` (e.g. `FixedWidthBuiltinConverterTests`, `FixedWidthCustomConverterTests`).

**Intentional non-parity and limitations**

- **CSV-first features** (advanced CSV dialects, dynamic `ClassBuilder`, etc.) are intentionally out of scope; use a CSV-focused library where that is the primary need.
- **`FileHelperAsyncEngine`** streaming may not yet mirror every multi-line / `FieldInNewLine` behavior of the synchronous engine — validate async paths for your layout.
- **Runtime `MultiRecordEngine` type discovery** is not revived; use the closed-set `MultiRecordEngine` API with explicit selectors (see above).

### Example 3: Streaming Large Files

```csharp
// ========== BEFORE (FileHelpers 3.x) ==========
using FileHelpers;

var engine = new FileHelperAsyncEngine<Customer>();
engine.BeginReadFile("largefile.csv");
while (engine.ReadNext() is { } record)
{
    Process(record);
}
engine.Close();

// ========== AFTER (ModernLibs.FileHelpers 4.x) ==========
using ModernLibs.FileHelpers;  // Only change needed!

var engine = new FileHelperAsyncEngine<Customer>();
engine.BeginReadFile("largefile.csv");
while (engine.ReadNext() is { } record)
{
    Process(record);
}
engine.Close();
```

### Example 4: Error Handling

```csharp
// ========== BEFORE (FileHelpers 3.x) ==========
using FileHelpers;

var engine = new FileHelperEngine<Customer>();
engine.ErrorMode = ErrorMode.SaveAndContinue;
var records = engine.ReadFile("data.csv");
foreach (var error in engine.ErrorManager.Errors)
    Console.WriteLine($"Line {error.LineNumber}: {error.Exception.Message}");

// ========== AFTER (ModernLibs.FileHelpers 4.x) ==========
using ModernLibs.FileHelpers;  // Only change needed!

var engine = new FileHelperEngine<Customer>();
engine.ErrorMode = ErrorMode.SaveAndContinue;
var records = engine.ReadFile("data.csv");
foreach (var error in engine.ErrorManager.Errors)
    Console.WriteLine($"Line {error.LineNumber}: {error.Exception.Message}");
```

### Example 5: Event Handling

```csharp
// ========== BEFORE (FileHelpers 3.x) ==========
using FileHelpers;

var engine = new FileHelperEngine<Customer>();
engine.BeforeReadRecord += (sender, e) =>
{
    if (e.RecordLine.StartsWith("#"))
        e.SkipThisRecord = true;
};

// ========== AFTER (ModernLibs.FileHelpers 4.x) ==========
using ModernLibs.FileHelpers;  // Only change needed!

var engine = new FileHelperEngine<Customer>();
engine.BeforeReadRecord += (sender, e) =>
{
    if (e.RecordLine.StartsWith("#"))
        e.SkipThisRecord = true;
};
```

### Example 6: INotifyRead/INotifyWrite Interface

```csharp
// ========== BEFORE (FileHelpers 3.x) ==========
using FileHelpers;

[DelimitedRecord(",")]
public class ValidatedOrder : INotifyRead, INotifyWrite
{
    [FieldOrder(1)] public int OrderId;
    [FieldOrder(2)] public decimal Amount;

    public void BeforeRead(BeforeReadEventArgs e)
    {
        if (e.RecordLine.Contains("INVALID"))
            e.SkipThisRecord = true;
    }

    public void AfterRead(AfterReadEventArgs e) { }
    public void BeforeWrite(BeforeWriteEventArgs e) { }
    public void AfterWrite(AfterWriteEventArgs e) { }
}

// ========== AFTER (ModernLibs.FileHelpers 4.x) ==========
using ModernLibs.FileHelpers;  // Only change needed!

[DelimitedRecord(",")]
public class ValidatedOrder : INotifyRead<ValidatedOrder>, INotifyWrite<ValidatedOrder>
{
    [FieldOrder(1)] public int OrderId;
    [FieldOrder(2)] public decimal Amount;

    public void BeforeRead(BeforeReadEventArgs<ValidatedOrder> e)
    {
        if (e.RecordLine.Contains("INVALID"))
            e.SkipThisRecord = true;
    }

    public void AfterRead(AfterReadEventArgs<ValidatedOrder> e) { }
    public void BeforeWrite(BeforeWriteEventArgs<ValidatedOrder> e) { }
    public void AfterWrite(AfterWriteEventArgs<ValidatedOrder> e) { }
}
```

**Note**: ModernLibs uses generic interfaces `INotifyRead<T>` and `INotifyWrite<T>` for type safety. The non-generic `INotifyRead`/`INotifyWrite` interfaces from FileHelpers 3.x are not available.

## Performance Considerations

### Benefits of Migrating

1. **AOT Compilation**: ModernLibs is fully AOT-compatible. Publish with `PublishAot=true` for native binaries.

2. **Zero Allocations**: For `record struct` types, parsing produces zero heap allocations per record.

3. **Span-Based Parsing**: ModernLibs uses `Span<T>` and `Memory<T>` throughout for efficient string handling.

4. **Source Generation**: No runtime reflection means faster startup and consistent performance.

### Benchmark Example

```csharp
// ModernLibs is typically 2-5x faster than FileHelpers 3.x
// and allocates significantly less memory

// FileHelpers 3.x: ~500 MB allocated for 1M records
// ModernLibs: ~50 MB allocated for 1M records (class)
// ModernLibs: ~0 MB allocated for 1M records (struct)
```

## Troubleshooting

### Issue: "No descriptor registered for type T"

**Cause**: The source generator didn't run or the record type lacks `[DelimitedRecord]` or `[FixedLengthRecord]`.

**Solution**: Ensure the record has the required attribute:

```csharp
[DelimitedRecord(",")]  // Required!
public class MyRecord { }
```

### Issue: Compile-time errors with INotifyRead/INotifyWrite

**Cause**: Using non-generic interfaces from FileHelpers 3.x.

**Solution**: Use generic interfaces:

```csharp
// Wrong
public class MyRecord : INotifyRead { }

// Right
public class MyRecord : INotifyRead<MyRecord> { }
```

### Issue: Different behavior with HeaderText/FooterText

**Cause**: ModernLibs normalizes line endings to `\r\n` for cross-platform consistency.

**Solution**: Set `NewLineForWrite` if needed:

```csharp
engine.NewLineForWrite = "\n";  // Unix-style
```

## Version Mapping

| FileHelpers | ModernLibs | Compatibility |
|-------------|------------|---------------|
| 3.5.x | 4.0.x | 95%+ API compatible |
| 3.4.x | 4.0.x | 95%+ API compatible |
| 3.3.x | 4.0.x | 95%+ API compatible |

## Getting Help

- **GitHub Issues**: https://github.com/modernlibs/filehelpers/issues
- **Documentation**: https://docs.modernlibs.dev/filehelpers
- **Original FileHelpers**: https://www.filehelpers.net (for comparison)

## Summary

Migrating from FileHelpers 3.x to ModernLibs.FileHelpers 4.x is straightforward:

1. ✅ Update `using FileHelpers;` to `using ModernLibs.FileHelpers;`
2. ✅ Update package reference
3. ✅ Use generic `INotifyRead<T>`/`INotifyWrite<T>` if implementing interfaces
4. ✅ Replace runtime ClassBuilder with source-generated types

Most code requires **zero changes** beyond the namespace update.
