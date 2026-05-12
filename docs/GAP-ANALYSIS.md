# ModernLibs.FileHelpers vs FileHelpers 3.x — Gap Analysis

> **Purpose**: Migration guide foundation. Documents what we match, what we intentionally dropped, what gaps exist, and what opt-in improvements we offer over FileHelpers 3.x.
>
> **Scope**: FileHelpers 3.5.2 (171 features across 18 modules) vs ModernLibs.FileHelpers 4.x (74 features across 11 modules).

---

## Executive Summary

| Category | Count | Details |
|---|---|---|
| **Full parity** | ~55 features | Core engines, attributes, converters, events, error handling, master-detail, multi-record |
| **Intentionally removed (AOT)** | 15 features | ClassBuilder, CsvEngine, non-generic constructors, SmartFormatDetector — all require runtime reflection |
| **Deferred (separate concern)** | 28 features | CommonEngine, Transform, BigFileSorter, FileDiff, DataLink, Excel — users can compose or use alternatives |
| **Gaps — reconsider candidates** | 12 features | Small APIs missing that could improve drop-in replacement quality without breaking AOT |
| **Opt-in improvements** | 16+ features | Zero-allocation paths, span converters, source generation, streaming, recycling, parallel reads |

**Drop-in replacement quality: ~85%** for the core record parsing/writing surface. Users of engines, attributes, converters, events, master-detail, and multi-record can migrate with a single `using` change plus the breaking changes documented in `MIGRATION.md`.

---

## 1. Full Parity — What Works Identically

These APIs behave the same as FileHelpers 3.x. Migration requires only `using FileHelpers;` → `using ModernLibs.FileHelpers;`.

### 1.1 Core Engines (`FileHelperEngine<T>`)

| 3.x API | Status | Notes |
|---|---|---|
| `ReadFile(path)` → `T[]` | ✅ Parity | |
| `ReadString(text)` → `T[]` | ✅ Parity | |
| `ReadStream(reader)` → `T[]` | ✅ Parity | |
| `ReadFile/ReadString/ReadStream(maxRecords)` | ✅ Parity | Cap at N records |
| `WriteFile(path, records)` | ✅ Parity | |
| `WriteString(records)` → `string` | ✅ Parity | |
| `WriteStream(writer, records)` | ✅ Parity | |
| `AppendToFile(path, record)` | ✅ Parity | |
| `AppendToFile(path, IEnumerable<T>)` | ✅ Parity | |
| `HeaderText` / `FooterText` | ✅ Parity | Captured from `IgnoreFirst`/`IgnoreLast` |
| `Encoding` property | ✅ Parity | Default changed to UTF-8 (see breaking changes) |
| `NewLineForWrite` property | ✅ Parity | |
| `ErrorMode` property | ✅ Parity | `ThrowException`, `IgnoreAndContinue`, `SaveAndContinue` |
| `ErrorManager` | ✅ Parity | `Errors`, `ErrorCount`, `HasErrors`, `ClearErrors()`, `SaveErrors()` |

### 1.2 Async Engine (`FileHelperAsyncEngine<T>`)

| 3.x API | Status | Notes |
|---|---|---|
| `BeginReadFile/BeginReadStream/BeginReadString` | ✅ Parity | |
| `ReadNext()` → `T?` | ✅ Parity | |
| `ReadNext(count)` → `T[]?` | ✅ Parity | Batch read |
| `BeginWriteFile/BeginWriteStream/BeginWriteString` | ✅ Parity | |
| `WriteNext(record)` / `WriteNext(IEnumerable<T>)` | ✅ Parity | |
| `Close()` / `Dispose()` | ✅ Parity | Idempotent, reusable |
| `IEnumerable<T>` (foreach) | ✅ Parity | |
| `IAsyncEnumerable<T>` ReadAsync | ✅ **Improvement** | Backed by `System.IO.Pipelines` with backpressure |

### 1.3 Record Attributes

| 3.x Attribute | Status | Notes |
|---|---|---|
| `[DelimitedRecord(delimiter)]` | ✅ Parity | |
| `[FixedLengthRecord(FixedMode)]` | ✅ Parity | |
| `[IgnoreFirst(N)]` | ✅ Parity | Captures as `HeaderText` |
| `[IgnoreLast(N)]` | ✅ Parity | Captures as `FooterText` |
| `[IgnoreEmptyLines]` | ✅ Parity | |
| `[IgnoreCommentedLines(char)]` | ✅ Parity | |
| `[ConditionalRecord(condition, pattern)]` | ✅ Parity | Regex validated at compile time |

### 1.4 Field Attributes

| 3.x Attribute | Status | Notes |
|---|---|---|
| `[FieldOrder(n)]` | ✅ Parity | |
| `[FieldFixedLength(n)]` | ✅ Parity | + `OverflowMode` (improvement) |
| `[FieldAlign(mode, padChar)]` | ✅ Parity | + `Center` mode (improvement) |
| `[FieldTrim(mode)]` | ✅ Parity | |
| `[FieldQuoted(char, mode)]` | ✅ Parity | |
| `[FieldDelimiter(string)]` | ✅ Parity | Per-field override |
| `[FieldInNewLine]` | ✅ Parity | Multi-line records |
| `[FieldHidden]` | ✅ Parity | |
| `[FieldNotInFile]` | ✅ Parity | Virtual field |
| `[FieldOptional]` | ✅ Parity | Trailing optional |
| `[FieldConverter(kind/type, args)]` | ✅ Parity | |
| `[FieldNullValue(value)]` | ✅ Parity | |
| `[FieldNotEmpty]` | ✅ Parity | |
| `[FieldValueDiscarded]` | ✅ Parity | |

### 1.5 Converters (`ConverterKind`)

| ConverterKind | Status | Notes |
|---|---|---|
| `None` (custom type) | ✅ Parity | |
| `Date` | ✅ Parity | |
| `DateMultiFormat` | ✅ Parity | |
| `Byte` / `SByte` | ✅ Parity | Culture-invariant |
| `Int16` / `Int32` / `Int64` | ✅ Parity | Culture-invariant |
| `UInt16` / `UInt32` / `UInt64` | ✅ Parity | Culture-invariant |
| `Decimal` | ✅ Parity | Culture-sensitive |
| `Double` / `Single` | ✅ Parity | Culture-sensitive |
| `PercentDouble` | ✅ Parity | |
| `Boolean` | ✅ Parity | Accepts true/false, 1/0 |
| `Char` | ✅ Parity | |
| `Guid` | ✅ Parity | |
| `Enum` | ✅ Parity | Name-based |
| `ConverterBase` (legacy bridge) | ✅ Parity | Bridged via `ConverterBaseAdapter<T>` |

### 1.6 Events

| 3.x Event | Status | Notes |
|---|---|---|
| `BeforeReadRecord` | ✅ Parity | + `StopReading` support |
| `AfterReadRecord` | ✅ Parity | |
| `BeforeWriteRecord` | ✅ Parity | |
| `AfterWriteRecord` | ✅ Parity | + line mutation |
| `ProgressChanged` | ✅ Parity | |
| `INotifyRead<T>` | ✅ Parity | Generic variant |
| `INotifyWrite<T>` | ✅ Parity | Generic variant |

### 1.7 Master-Detail

| 3.x API | Status | Notes |
|---|---|---|
| `MasterDetailEngine<TMaster, TDetail>` | ✅ Parity | |
| `MasterDetails<TMaster, TDetail>` | ✅ Parity | |
| `MasterDetailSelector` delegate | ✅ Parity | |
| `RecordAction` (Master/Detail/Skip) | ✅ Parity | |
| Read/Write File/Stream/String | ✅ Parity | |

### 1.8 Multi-Record

| 3.x API | Status | Notes |
|---|---|---|
| `MultiRecordEngine` | ✅ Parity | AOT-safe closed-set API |
| `IMultiRecordType` | ✅ Parity | |
| `MultiRecordType.For<T>(selector)` | ✅ Parity | |
| Read/Write File/Stream/String | ✅ Parity | |

---

## 2. Intentionally Removed — AOT-Incompatible

These features were **deliberately dropped** because they rely on runtime reflection, `Type` objects at runtime, or dynamic code generation — all incompatible with Native AOT and IL trimming.

### 2.1 Dynamic Class Building (`FileHelpers.Dynamic` namespace) — 15 features

| 3.x Feature | Why Removed | Migration Path |
|---|---|---|
| `ClassBuilder` (compile from source string/file) | Uses `CSharpCodeProvider` / `VBCodeProvider` at runtime | Use source-generated record types with `[DelimitedRecord]`/`[FixedLengthRecord]` |
| `ClassBuilder` (from binary/XML) | Runtime deserialization + compilation | Define types statically |
| `DelimitedClassBuilder` | Fluent runtime builder | Define class with attributes |
| `FixedLengthClassBuilder` | Fluent runtime builder | Define class with attributes |
| `CsvClassBuilder` | Convenience builder over ClassBuilder | Define class with `[DelimitedRecord(",")]` |
| `ConverterBuilder` | Runtime attribute attachment | Use `[FieldConverter]` attribute |
| `FieldBuilder` / `DelimitedFieldBuilder` / `FixedFieldBuilder` | Runtime field construction | Define fields as properties/fields |
| `ClassBuilder.SaveToBinary/SaveToXml/SaveClassSource` | Persistence of runtime-generated types | Not needed — types are in source code |
| `CsvEngine` | Runtime CSV engine built on ClassBuilder | Use `FileHelperEngine<T>` with a statically defined record |
| `CsvOptions` | Configuration for CsvEngine | Set properties on engine |
| `NetLanguage` / `NetVisibility` enums | Code generation metadata | Not applicable |

**Assessment: Correct decision.** The entire Dynamic namespace is antithetical to AOT. The source generator (`IIncrementalGenerator`) is a superior replacement — it produces the same result at compile time with zero runtime cost and full trimmer safety.

### 2.2 Non-Generic Engine Constructors

```csharp
// 3.x — NOT SUPPORTED
var engine = new FileHelperEngine(typeof(Customer));
var engine = new FileHelperAsyncEngine(typeof(Customer));
```

**Why removed:** Requires `Activator.CreateInstance(typeof(FileHelperEngine<>).MakeGenericType(type))` — breaks IL trimming, prevents source-generated descriptor resolution.

**Migration:** Use `new FileHelperEngine<Customer>()`. If dynamic dispatch is needed, cache closed-generic engines at startup.

**Assessment: Correct decision.** The generic form is the primary API in 3.x anyway. The non-generic form was a convenience for dynamic scenarios that are better served by the source generator.

### 2.3 SmartFormatDetector (`FileHelpers.FormatDetector`) — 4 features

| 3.x Feature | Why Removed |
|---|---|
| `SmartFormatDetector.DetectFileFormat()` | Heuristic delimiter detection depends on `ClassBuilder` (Dynamic namespace) for result generation |
| `RecordFormatInfo` | Carries a `ClassBuilder` — useless without Dynamic |
| `DelimiterInfo` / `QuoteHelper` | Internal to SmartFormatDetector |
| `FormatHint` enum | Bias detector toward delimited/fixed |

**Assessment: Correct decision.** The format detector exists to auto-generate record classes at runtime. With the source generator approach, users define types explicitly. For users who need format detection, third-party libraries (Sep, CsvHelper) offer better heuristics.

### 2.4 Non-Generic `INotifyRead` / `INotifyWrite`

```csharp
// 3.x — NOT SUPPORTED
public class MyRecord : INotifyRead {
    public void BeforeRead(BeforeReadEventArgs e) { ... }
}

// ModernLibs — required
public class MyRecord : INotifyRead<MyRecord> {
    public void BeforeRead(BeforeReadEventArgs<MyRecord> e) { ... }
}
```

**Why changed:** Non-generic interfaces require boxing and `object`-typed record access. The generic variant provides type safety and zero boxing.

**Assessment: Correct decision.** Breaking change is minor (add `<T>` to interface name) and provides tangible benefits.

---

## 3. Deferred — Separate Concern

These features existed in FileHelpers 3.x but are not included here. They are not AOT-incompatible — they were omitted because they are separate concerns with better alternatives in the modern .NET ecosystem, or are legacy .NET Framework-only features.

### 3.1 CommonEngine (16 features)

Static one-call helpers for routine tasks:

| 3.x Feature | Why Deferred | Alternative |
|---|---|---|
| `CommonEngine.ReadFile<T>(path)` | Trivial wrapper | `new FileHelperEngine<T>().ReadFile(path)` |
| `CommonEngine.ReadString<T>(text)` | Trivial wrapper | `new FileHelperEngine<T>().ReadString(text)` |
| `CommonEngine.WriteFile<T>(path, records)` | Trivial wrapper | `new FileHelperEngine<T>().WriteFile(path, records)` |
| `CommonEngine.WriteString<T>(records)` | Trivial wrapper | `new FileHelperEngine<T>().WriteString(records)` |
| `CommonEngine.ReadSortedFile<T>(path)` | Sort is deferred | Read + `Array.Sort()` + write |
| `CommonEngine.SortFile<T>(path)` | Sort is deferred | Read + `Array.Sort()` + write |
| `CommonEngine.SortByField<T>(path, fieldName)` | Reflection-based | Sort in user code |
| `CommonEngine.SortRecords<T>(records)` | Sort is deferred | `Array.Sort(records)` |
| `CommonEngine.MergeFiles<T>(path1, path2, destPath)` | Trivial composition | Read both + write combined |
| `CommonEngine.MergeAndSortFiles<T>(path1, path2, destPath)` | Sort is deferred | Merge + sort + write |
| `CommonEngine.RemoveDuplicates<T>(records)` | Trivial | `records.Distinct().ToArray()` |
| `CommonEngine.RecordsToDataTable(records)` | DataTable is legacy | Use user-defined projection |
| `CommonEngine.DataTableToCsv(table, path)` | DataTable is legacy | Use `System.Text.Json` or other |
| `CommonEngine.CsvToDataTable(path)` | DataTable is legacy | Use a CSV library |
| `CommonEngine.ReadCsv(path)` | CSV is not our focus | Use Sep, CsvHelper, or Sylvan |
| `CommonEngine.TransformFile / TransformFileFast` | Transform is deferred | See Transform section |

**Assessment: Correct decision.** These are convenience wrappers, not core functionality. Users can compose them from the engine API in 1-2 lines. Adding them would bloat the library without adding capability.

### 3.2 FileTransformEngine (6 features)

| 3.x Feature | Why Deferred | Alternative |
|---|---|---|
| `FileTransformEngine<TSource, TDestination>` | Requires `ITransformable<TDestination>` on source type | User code: read source → project → write destination |
| `TransformFast` (streaming) | Low-value optimization | Stream read + stream write in user code |
| `Transform` (in-memory) | Trivial | `source.Select(s => s.Transform()).ToArray()` |
| `ReadAndTransform` | Composition of read + transform | Two-step: read then project |
| `ITransformable<TDestination>` | Interface on record type | User-defined projection method |
| Separate source/destination ErrorManager | Complexity for niche feature | Handle errors per-engine |

**Assessment: Correct decision.** The transform engine solves a problem that LINQ solves more elegantly. The `ITransformable` interface forces mutation of the source record type. Users can project between types without any library support.

### 3.3 BigFileSorter (2 features)

| 3.x Feature | Why Deferred | Alternative |
|---|---|---|
| `BigFileSorter` (text lines) | External sort is a separate concern | Use OS `sort` command or dedicated tool |
| `BigFileSorter<T>` (typed records) | External sort is a separate concern | Read chunks → sort → merge |

**Assessment: Correct decision.** External sorting is a well-solved problem with better dedicated tools. The FileHelpers implementation is basic k-way merge sort.

### 3.4 FileDiffEngine (5 features)

| 3.x Feature | Why Deferred | Alternative |
|---|---|---|
| `OnlyNew(newFile, sourceFile)` | Set operations on records | LINQ `Except` |
| `OnlyMissing(newFile, sourceFile)` | Set operations on records | LINQ `Except` (reversed) |
| `OnlyDuplicated(newFile, sourceFile)` | Set operations on records | LINQ `Intersect` |
| `OnlyNonDuplicated(newFile, sourceFile)` | Set operations on records | LINQ `SymmetricExceptWith` |
| `WriteNew(newFile, sourceFile, destPath)` | Composition | Read → Except → Write |

**Assessment: Correct decision.** These are thin wrappers around `IComparable<T>` set operations. LINQ handles this natively.

### 3.5 DataLink (4 features) — .NET Framework Only

| 3.x Feature | Why Deferred |
|---|---|
| `FileDataLink` | Legacy .NET Framework data binding |
| `GenericDataLink` | Abstract base for custom storage |
| `ExcelStorageBase` | Excel via COM Interop |
| `TransactionMode` | Transaction semantics for storage |

**Assessment: Correct decision.** DataLink was never in the .NET Standard build. It relies on `System.Data`, COM interop, and .NET Framework-specific APIs. Modern .NET has entirely different data access patterns (Dapper, EF Core, etc.).

### 3.6 Excel NPOI Storage (1 feature) — Separate Package

| 3.x Feature | Why Deferred |
|---|---|
| `ExcelNPOIStorage` | NPOI dependency, .NET Framework only |

**Assessment: Correct decision.** Excel support belongs in a separate package. NPOI is a heavy dependency that most users don't need.

### 3.7 DataTable Interop (obsolete in 3.x)

| 3.x Feature | Status |
|---|---|
| `ReadAsDataTable()` | Marked `[Obsolete]` in 3.x |
| `WriteFromDataTable()` | Marked `[Obsolete]` in 3.x |
| `RecordsToDataTable()` | Available via `ExtensionsFileHelpers` |

**Assessment: Correct decision.** `DataTable` is a legacy .NET Framework concept. Modern .NET applications use typed records, not `DataTable`.

---

## 4. Gaps — Reconsider Candidates

These are features that **could be added without breaking AOT** and would improve the drop-in replacement story. Each is evaluated for use case, effort, and recommendation.

### 4.1 `DelimitedFileEngine<T>` / `FixedFileEngine<T>` — Runtime Options

**3.x API:**
```csharp
var engine = new DelimitedFileEngine<Customer>();
engine.Options.Delimiter = ";";  // Runtime change

var engine = new FixedFileEngine<Employee>();
engine.Options.FixedMode = FixedMode.AllowMoreChars;  // Runtime change
```

**Use case:** Users who need to change delimiter or fixed-length options at runtime without recompiling. Common in data integration tools where the file format is determined by configuration.

**Gap:** We have `RecordOptions` as a `readonly struct` on the descriptor. Changing options at runtime would require regenerating the descriptor, which conflicts with the source-generated model.

**Reconsider? ⚠️ Maybe.** Could expose mutable options on the engine that override the descriptor's options at parse time. This is a moderate effort change but would help users migrating from `DelimitedFileEngine`/`FixedFileEngine`.

**Workaround:** Define multiple record types with different options, or use events to manipulate the input line.

### 4.2 `IFileHelperEngine<T>` / `IFileHelperAsyncEngine<T>` Interfaces

**3.x API:**
```csharp
IFileHelperEngine<Customer> engine = new FileHelperEngine<Customer>();
```

**Use case:** Users who want to mock the engine in tests, or accept engines as dependencies via interface injection.

**Gap:** We don't expose these interfaces. Users must depend on concrete `FileHelperEngine<T>`.

**Reconsider? ✅ Yes, low effort.** Define the interfaces matching the 3.x signatures. This is pure additive — no breaking changes, no AOT impact. Enables DI and testing patterns.

### 4.3 `FieldArrayLength` Attribute

**3.x API:**
```csharp
[FieldArrayLength(3, 10)]
public decimal[] Prices;  // Array with 3-10 elements
```

**Use case:** Files where a single record contains a variable-length array of values (e.g., monthly prices, quarterly scores). The attribute specifies min/max bounds.

**Gap:** We don't support array fields. Each field maps to a single value.

**Reconsider? ⚠️ Maybe, moderate effort.** Array fields require special parsing logic — the engine needs to know how many delimited segments to consume. For fixed-length, it needs to know the per-element width. This is a niche feature but could be valuable for financial data imports.

**Workaround:** Parse as a single string field and split manually in a post-processing step.

### 4.4 `FieldCaption` Attribute

**3.x API:**
```csharp
[FieldCaption("Customer Name")]
public string Name;
```

**Use case:** Overrides the field name used in `GetFileHeader()` for generating column headers. Useful when the property name doesn't match the desired column header.

**Gap:** We don't have this attribute. Header generation uses the field/property name directly.

**Reconsider? ✅ Yes, trivial effort.** The attribute is a simple string storage. The generator already reads field metadata — adding caption support is a one-line change in the header generation path.

### 4.5 `IgnoreInheritedClass` Attribute

**3.x API:**
```csharp
public class BaseRecord {
    public int Id;
}

[IgnoreInheritedClass]
[DelimitedRecord(",")]
public class DerivedRecord : BaseRecord {
    public string Name;  // Only Name is in the file
}
```

**Use case:** Record types that inherit from a base class but only want to map the derived class's fields to the file. The base class fields are ignored by the engine.

**Gap:** The source generator currently processes all fields including inherited ones. There's no way to exclude inherited fields.

**Reconsider? ✅ Yes, low effort.** The generator already walks the inheritance hierarchy. Adding a check for `[IgnoreInheritedClass]` on the derived type would skip base class fields. Useful for users with shared base record types.

### 4.6 `CultureConverter` — Explicit CultureInfo

**3.x API:**
```csharp
[FieldConverter(typeof(CultureConverter), "fr-FR")]
public decimal Amount;  // Uses French number format (comma as decimal separator)
```

**Use case:** Files that use locale-specific number/date formats (European decimal commas, Japanese date formats, etc.). The `CultureConverter` wraps numeric/date converters with an explicit `CultureInfo`.

**Gap:** Our converters use `CultureInfo.InvariantCulture` by default. There's no way to specify a culture per field.

**Reconsider? ✅ Yes, moderate effort.** This is important for international data. The current converters could accept an optional culture parameter. The `ConverterKind` enum could add a `Culture` variant, or the existing converters could accept culture as an additional arg.

**Impact:** High for users processing non-English data. Files with `42,5` (European decimal) would fail to parse as `42.5`.

### 4.7 `CommonSelector` Presets for Master-Detail

**3.x API:**
```csharp
var engine = new MasterDetailEngine<Order, OrderLine>(
    CommonSelector.BeginsWith, "ORD", "LIN");
```

**Use case:** Common patterns for master-detail classification without writing a custom selector delegate. The presets are: `HasLengthLessThan`, `HasLengthMoreThan`, `BeginsWith`, `EndsWith`, `Equals`, `Contains`.

**Gap:** We only support custom `MasterDetailSelector` delegates. No preset helpers.

**Reconsider? ✅ Yes, trivial effort.** These are simple static factory methods that return a `MasterDetailSelector` delegate. No AOT impact, no breaking changes. Improves ergonomics for the common case.

### 4.8 `RecordAction.DetailCascade`

**3.x API:**
```csharp
RecordAction.DetailCascade  // Detail records that cascade from previous master
```

**Use case:** Multi-level hierarchies where a detail record can itself act as a master for sub-details. The `DetailCascade` action tells the engine to treat the record as a detail of the current master while also starting a new detail group.

**Gap:** We only support `Master`, `Detail`, `Skip`. No `DetailCascade`.

**Reconsider? ❌ No.** This is a niche feature for deeply nested hierarchies. Most master-detail files are two-level. Users who need this can implement it with a custom selector that manages state.

### 4.9 `ReadRemaining()` on Async Engine

**3.x API:**
```csharp
asyncEngine.BeginReadFile("data.csv");
T[] all = asyncEngine.ReadRemaining();  // Read all remaining at once
```

**Use case:** After reading some records individually with `ReadNext()`, read the rest in bulk.

**Gap:** We don't have this method. Users must loop `ReadNext()` until null.

**Reconsider? ✅ Yes, trivial effort.** Simple loop internally. Matches 3.x API exactly.

### 4.10 `BeginAppendFile()` on Async Engine

**3.x API:**
```csharp
asyncEngine.BeginAppendFile("data.csv");  // Append mode
asyncEngine.WriteNext(record);
```

**Use case:** Append records to an existing file one at a time without loading it.

**Gap:** Our `FileHelperAsyncEngine` supports `BeginWriteFile` (overwrite) but not `BeginAppendFile`.

**Reconsider? ✅ Yes, low effort.** Open the file with `FileMode.Append` instead of `FileMode.Create`. Matches 3.x API.

### 4.11 `LastRecordValue` / `LastRecordValues` on Async Engine

**3.x API:**
```csharp
asyncEngine.ReadNext();
object last = asyncEngine.LastRecordValue;  // Last record as object
string[] values = asyncEngine.LastRecordValues;  // Raw field values as strings
```

**Use case:** Access the last-read record without storing it, or access the raw string values before conversion (useful for logging/debugging).

**Gap:** We don't expose these properties.

**Reconsider? ❌ No.** `LastRecordValue` is redundant — the user already has the record from `ReadNext()`. `LastRecordValues` exposes internal parsing state that leaks abstraction. Both are legacy patterns that don't fit the modern API.

### 4.12 By-Values Overload on `WriteNext`

**3.x API:**
```csharp
asyncEngine.WriteNext("value1", "value2", 42);  // Write by field values
```

**Use case:** Write a record without constructing the record object. Useful when data comes from a different source (database row, API response) and constructing a record object is overhead.

**Gap:** We require a `T` instance for `WriteNext(T)`.

**Reconsider? ❌ No.** This bypasses type safety and requires the engine to accept `object[]` or `string[]` and map to fields. It's a legacy pattern that conflicts with the typed, source-generated model. Users can construct the record type — it's a single `new` call.

---

## 5. Opt-In Improvements — What We Offer Beyond 3.x

These are features that **do not exist in FileHelpers 3.x** and represent our value proposition for migrating.

### 5.1 Zero-Allocation Record Parsing

| Feature | Description |
|---|---|
| `record struct` types | Zero per-record heap allocations. The parser writes directly into stack-allocated structs. |
| `[SpanRecord]` on `ref struct` | Stack-only records. Cannot be boxed, captured, or stored. Maximum performance. |
| `SpanRecordCallback<T>` | `delegate void(ref T record)` — pass records by reference to avoid struct copying. |

**Impact:** For 1M records, FileHelpers 3.x allocates ~500 MB. ModernLibs with `record struct` allocates ~0 MB per record.

### 5.2 Span-Based Converters (`ISpanConverter<T>`)

| Feature | Description |
|---|---|
| `ISpanConverter<T>` | `TryParse(ReadOnlySpan<char>, out T)` — no string allocation during parsing. |
| `TryFormat(T, Span<char>, out int)` | Write directly into a span — no string allocation during formatting. |
| `ConverterBaseAdapter<T>` | Bridges legacy `ConverterBase` into the span pipeline (1 string alloc per call for migration compatibility). |

**Impact:** Eliminates the `ToString()` and `string` parsing allocations that dominated 3.x's GC pressure.

### 5.3 Source-Generated Descriptors

| Feature | Description |
|---|---|
| `IIncrementalGenerator` | Descriptors are generated at compile time. No reflection, no `TypeDescriptor`, no `Expression<>`. |
| `[ModuleInitializer]` registry | Descriptors auto-register into `FileHelperRegistry<T>` before user code runs. |
| Compile-time diagnostics | `MLFH001`–`MLFH005` catch configuration errors at build time, not runtime. |

**Impact:** Faster startup, AOT-compatible, deterministic behavior, no runtime reflection overhead.

### 5.4 Record Recycling

| Feature | Description |
|---|---|
| `RecycleRecords` on engine | Reuses a single class instance across all callback invocations. |
| `IRecyclableDescriptor<T>.ParseInto` | Fills an existing instance instead of allocating a new one. |
| `RecordPool<T>` | Manual pooling for cross-invocation reuse. |

**Impact:** Class types can achieve near-zero allocation per record when combined with callback-based reads.

### 5.5 Modern Streaming

| Feature | Description |
|---|---|
| `IAsyncEnumerable<T>` | `await foreach` over records with backpressure via `System.IO.Pipelines`. |
| `ReadAsync(path, ct)` | Cancellation token support. |
| Pipeline-based line reading | No need to load entire file into memory before parsing. |

**Impact:** Process multi-gigabyte files with constant memory footprint.

### 5.6 Callback-Based Reads

| Feature | Description |
|---|---|
| `ReadFile(path, Action<T>)` | No `T[]` result array allocation. |
| `ReadSpan(span, Action<T>)` | Zero content allocation from span input. |
| `ReadSpan(span, SpanRecordCallback<T>)` | Ref callback — no struct copy. |

**Impact:** Eliminates the result array allocation (O(N) for N records). Combined with `record struct`, the entire read path is allocation-free.

### 5.7 Parallel Read Extensions

| Feature | Description |
|---|---|
| `FileHelperEngineParallelExtensions` | Parallel processing of records across multiple cores. |

**Impact:** Utilize all CPU cores for CPU-bound record processing.

### 5.8 Additional Converters

| Converter | Description |
|---|---|
| `DateOnlyConverter` | Parse/format `DateOnly` (new in .NET 6). |
| `TimeOnlyConverter` | Parse/format `TimeOnly` (new in .NET 6). |
| `StringConverter` | Identity span copy for string fields. |

**Impact:** Native support for modern .NET types that didn't exist in the FileHelpers 3.x era.

### 5.9 Enhanced Fixed-Width Controls

| Feature | Description |
|---|---|
| `OverflowMode` on `[FieldFixedLength]` | `DiscardEnd` (silent truncation) or `Error` (throw on overflow). |
| `AlignMode.Center` | Center alignment for fixed-length output (3.x only had Left/Right). |

**Impact:** Fine-grained control over data loss behavior and output formatting.

### 5.10 Event Enhancements

| Feature | Description |
|---|---|
| `StopReading` in `BeforeReadEventArgs` | Signal the engine to stop reading entirely (not just skip one record). |
| `RecordLineChanged` in `BeforeReadEventArgs` | Mutate the input line before parsing. |
| Line mutation in `AfterWriteEventArgs` | Mutate the formatted line before writing. |

**Impact:** More powerful event-based customization without implementing record interfaces.

---

## 6. Known Limitations

These are behaviors that differ from 3.x and are documented as limitations:

| Limitation | Impact | Workaround |
|---|---|---|
| `IgnoreLast` not honored in `ReadAsync` | Trailing lines parsed as data | Use `ReadFile` when `IgnoreLast` is required |
| `IgnoreLast` not honored in `ReadSync` | Same as above | Use `ReadFile` |
| `INotifyRead<T>.BeforeRead` not called during reads | Record doesn't exist before parsing | Only `AfterRead` fires for interface callbacks |
| Default encoding is UTF-8 (not system ANSI) | Legacy encoded files may garble | Set `engine.Encoding = Encoding.GetEncoding(1252)` |
| Multi-record requires closed-set configuration | No runtime type discovery | Use `MultiRecordType.For<T>(selector)` explicitly |
| No `BeginAppendFile` on async engine | Cannot append asynchronously | Use sync `AppendToFile` or open file manually |

---

## 7. Migration Decision Matrix

Use this matrix to determine if migration is right for your project:

| Your Usage | Migration Effort | Recommendation |
|---|---|---|
| `FileHelperEngine<T>` with basic read/write | Trivial (1 line) | ✅ Migrate immediately |
| `FileHelperAsyncEngine<T>` streaming | Trivial (1 line) | ✅ Migrate immediately |
| Master-detail processing | Trivial (1 line) | ✅ Migrate immediately |
| Multi-record with explicit types | Trivial (1 line) | ✅ Migrate immediately |
| Events (`BeforeRead`, `AfterRead`, etc.) | Low (generic interfaces) | ✅ Migrate — get `StopReading` bonus |
| Custom `ConverterBase` converters | Low (bridge is automatic) | ✅ Migrate — consider `ISpanConverter<T>` for perf |
| `INotifyRead`/`INotifyWrite` on records | Low (add `<T>`) | ✅ Migrate |
| `DelimitedFileEngine<T>` runtime options | Medium (define multiple types) | ⚠️ Evaluate — check if runtime changes are needed |
| `FieldCaption` for headers | Low (manual header or add attribute) | ⚠️ Evaluate — trivial to add |
| `CommonEngine` static helpers | Low (wrap engine yourself) | ✅ Migrate — helpers are 1-liners |
| `FileTransformEngine` | Medium (use LINQ projection) | ✅ Migrate — LINQ is cleaner |
| `BigFileSorter` | Medium (use external tool) | ✅ Migrate — better tools exist |
| `FileDiffEngine` | Low (use LINQ set ops) | ✅ Migrate — LINQ is native |
| `ClassBuilder` / dynamic types | High (rewrite to static types) | ⚠️ Evaluate — source generator is the replacement |
| `CsvEngine` (no record type) | High (define record type) | ⚠️ Evaluate — consider CsvHelper for CSV-only |
| `SmartFormatDetector` | High (define types manually) | ⚠️ Evaluate — define types explicitly |
| `DataLink` / database integration | High (use modern data access) | ❌ Don't migrate yet — use Dapper/EF Core |
| Excel NPOI storage | High (separate package needed) | ❌ Don't migrate yet — not available |

---

## 8. Summary

### What we match (85%+ of core API)
- All core engines (sync, async, master-detail, multi-record)
- All record and field attributes (except `FieldArrayLength`, `FieldCaption`, `IgnoreInheritedClass`)
- All built-in converters (plus `DateOnly`, `TimeOnly`, `StringConverter`)
- Full event model (with enhancements)
- Error handling (`ErrorMode`, `ErrorManager`)
- Master-detail and multi-record processing

### What we intentionally removed (AOT-incompatible)
- Dynamic class building (`ClassBuilder`, `CsvEngine`)
- Non-generic engine constructors
- `SmartFormatDetector`
- Non-generic `INotifyRead`/`INotifyWrite`

### What we deferred (separate concern)
- `CommonEngine` static helpers
- `FileTransformEngine`
- `BigFileSorter`
- `FileDiffEngine`
- `DataLink` (legacy .NET Framework)
- Excel NPOI storage

### What we should reconsider (gaps)
- `IFileHelperEngine<T>` / `IFileHelperAsyncEngine<T>` interfaces — **low effort, high value**
- `FieldCaption` attribute — **trivial effort**
- `IgnoreInheritedClass` attribute — **low effort**
- `CommonSelector` presets — **trivial effort**
- `ReadRemaining()` on async engine — **trivial effort**
- `BeginAppendFile()` on async engine — **low effort**
- `CultureConverter` / per-field culture — **moderate effort, high value for intl data**
- `DelimitedFileEngine<T>` runtime options — **moderate effort**
- `FieldArrayLength` — **moderate effort, niche**

### What we offer beyond 3.x
- Zero-allocation `record struct` parsing
- Span-based converters (`ISpanConverter<T>`)
- Source-generated descriptors (compile-time, no reflection)
- Record recycling for class types
- `IAsyncEnumerable<T>` streaming with backpressure
- Callback-based reads (no result array allocation)
- Ref struct records (`[SpanRecord]`)
- Parallel read extensions
- Compile-time diagnostics
- `DateOnly`/`TimeOnly` converters
- `OverflowMode` for fixed-width overflow handling
- `AlignMode.Center`
- Event enhancements (`StopReading`, `RecordLineChanged`)
