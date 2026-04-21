# ModernLibs.FileHelpers — Feature Inventory by Module

<!-- generated-by: fl:feature-inventory -->
<!-- sidecar: docs/feature-inventory.index.toon -->

## How to use

This document enumerates every user-reachable surface in ModernLibs.FileHelpers — 66 features in total — grouped first by module (cohesive area of the public API) and then by feature group. IDs are stable across regenerations: the machine-readable index lives at `docs/feature-inventory.index.toon` and can be queried with `og`, `awk -F'|'`, or `grep`. Module names mirror the legacy FileHelpers 3.x taxonomy where features survived the AOT rewrite; a `Migration parity` section below tracks the legacy modules that are not present in this codebase.

The `Status` column is `active` for everything currently in `src/`. The `Type` column applies the skill's enum (`crud`, `list`, `validation`, `report`, `integration`, `lookup`, `navigation`) to a library API surface — the closest semantic match was chosen per feature. The `UI placement` column is `—` because this is a class library with no UI.

## Executive summary

| Module | Feature groups | Active | Retired | Stub | Orphan | Total |
|---|---:|---:|---:|---:|---:|---:|
| ANALYZER | 1 | 5 | 0 | 0 | 0 | 5 |
| ASYNC | 2 | 8 | 0 | 0 | 0 | 8 |
| ATTRIBUTES | 1 | 7 | 0 | 0 | 0 | 7 |
| CONVERTERS | 2 | 8 | 0 | 0 | 0 | 8 |
| ENGINES | 1 | 9 | 0 | 0 | 0 | 9 |
| ERRORS | 1 | 4 | 0 | 0 | 0 | 4 |
| EVENTS | 2 | 7 | 0 | 0 | 0 | 7 |
| FIELDS | 3 | 14 | 0 | 0 | 0 | 14 |
| GENERATOR | 1 | 4 | 0 | 0 | 0 | 4 |
| **Total** | **14** | **66** | **0** | **0** | **0** | **66** |

## Migration parity

These legacy FileHelpers 3.x modules are not present in v1.1. See `docs/MIGRATION.md` for the full compatibility matrix.

| Legacy module | Status | Reason |
|---|---|---|
| MULTI-RECORD | not in v1.1 | engines for files mixing multiple record types |
| MASTER-DETAIL | deferred (P2) | parent/child grouped records |
| TRANSFORM | not in v1.1 | file-to-file type-to-type transformation engine |
| COMMON | not in v1.1 | static one-call helpers (`CommonEngine`); users wrap `FileHelperEngine<T>` directly |
| DYNAMIC | removed | `ClassBuilder` / `CsvEngine` rely on runtime reflection — incompatible with AOT. Replaced by GENERATOR. |
| FORMAT-DETECT | not in v1.1 | `SmartFormatDetector` heuristic delimiter detection |
| SORT | not in v1.1 | external sort for big files |
| DIFF | not in v1.1 | `FileDiff` engine |
| DATALINK | not in v1.1 | legacy storage round-trip |
| EXCEL-NPOI | separate package | NPOI-backed Excel storage |

---

## ANALYZER

Compile-time diagnostics emitted by the source generator (`MLFH001`–`MLFH005`). Catches missing record attributes, missing or zero fixed-length widths, ambiguous `FieldOrder` values, unrecognised `FieldConverter` types, and invalid `ConditionalRecord` regex patterns before runtime. Diagnostics live in the generator project but are surfaced to the user as build errors/warnings, mirroring the legacy `FileHelpers.Analyzer` package.

### Compile-time diagnostics

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| ANALYZER-01 | MLFH001 missing record attribute | Engine or registry used with a type missing `[DelimitedRecord]` or `[FixedLengthRecord]`. | — | active | validation | src/ModernLibs.FileHelpers.Generator/Diagnostics/DiagnosticDescriptors.cs, src/ModernLibs.FileHelpers.Generator/FileHelpersGenerator.cs |
| ANALYZER-02 | MLFH002 missing or zero FieldFixedLength | A field in a `[FixedLengthRecord]` type lacks `[FieldFixedLength]` or declares a non-positive width. | — | active | validation | src/ModernLibs.FileHelpers.Generator/Diagnostics/DiagnosticDescriptors.cs, src/ModernLibs.FileHelpers.Generator/FileHelpersGenerator.cs |
| ANALYZER-03 | MLFH003 ambiguous FieldOrder | Two or more fields share the same `[FieldOrder]`; descriptor emission is suppressed. | — | active | validation | src/ModernLibs.FileHelpers.Generator/Diagnostics/DiagnosticDescriptors.cs, src/ModernLibs.FileHelpers.Generator/FileHelpersGenerator.cs |
| ANALYZER-04 | MLFH004 unrecognised FieldConverter type | `[FieldConverter(typeof(X))]` references a type that is neither `ConverterBase` nor `ISpanConverter<T>`. | — | active | validation | src/ModernLibs.FileHelpers.Generator/Diagnostics/DiagnosticDescriptors.cs, src/ModernLibs.FileHelpers.Generator/FileHelpersGenerator.cs |
| ANALYZER-05 | MLFH005 invalid ConditionalRecord regex | `ConditionalRecord` regex pattern fails to compile at build time. | — | active | validation | src/ModernLibs.FileHelpers.Generator/Diagnostics/DiagnosticDescriptors.cs, src/ModernLibs.FileHelpers.Generator/FileHelpersGenerator.cs |

---

## ASYNC

Forward-only streaming engine. `FileHelperAsyncEngine<T>` exposes both the legacy pull (`BeginRead*` / `ReadNext`) and push (`BeginWrite*` / `WriteNext`) API plus a modern `IAsyncEnumerable<T> ReadAsync` path backed by `System.IO.Pipelines`. Class records can opt into per-record recycling for zero-allocation reads.

### Streaming read & write

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| ASYNC-01 | Begin streaming read | `BeginReadFile` / `BeginReadStream` / `BeginReadString` open a forward-only read session. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs |
| ASYNC-02 | Pull next record | `ReadNext()` returns one record or null at EOF; `ReadNext(count)` batches up to N. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs |
| ASYNC-03 | IAsyncEnumerable read | `ReadAsync(filePath, ct)` streams via `System.IO.Pipelines` and `PipelineLineReader`. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs, src/ModernLibs.FileHelpers/Parsing/PipelineLineReader.cs |
| ASYNC-04 | Begin streaming write | `BeginWriteFile` / `BeginWriteStream` / `BeginWriteString` plus `EndWriteString` to retrieve the buffered string. | — | active | report | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs |
| ASYNC-05 | Push record | `WriteNext(T)` and `WriteNext(IEnumerable<T>)` format and emit records during an open write session. | — | active | report | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs |
| ASYNC-06 | Close or Dispose session | `Close()` and `Dispose()` are idempotent; the engine is reusable for a fresh `BeginRead*` / `BeginWrite*` session afterwards. | — | active | navigation | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs |
| ASYNC-07 | IEnumerable<T> foreach | `GetEnumerator` yields records over an open `BeginRead*` session, supporting `foreach`. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs |

### Record recycling

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| ASYNC-08 | RecycleRecords | When set on a class record session, `ReadNext` reuses one `T` instance per call via `IRecyclableDescriptor<T>.ParseInto` for zero-alloc reads. | — | active | navigation | src/ModernLibs.FileHelpers/Engines/FileHelperAsyncEngine.cs, src/ModernLibs.FileHelpers/Descriptors/IRecyclableDescriptor.cs |

---

## ATTRIBUTES

Record-level annotations consumed by the source generator at compile time. They declare the file shape (delimited or fixed-length), header/footer skip rules, comment markers, and conditional include/exclude filters.

### Record-level attributes

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| ATTRIBUTES-01 | [DelimitedRecord(delim)] | Declares a delimiter-separated record (CSV/TSV/pipe). `MultilineMode` opt-in enables RFC 4180 quoted multiline fields. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/DelimitedRecordAttribute.cs, src/ModernLibs.FileHelpers/Enums/MultilineMode.cs |
| ATTRIBUTES-02 | [FixedLengthRecord(FixedMode)] | Declares a fixed-width record. `FixedMode` controls how short / long lines are handled. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/FixedLengthRecordAttribute.cs, src/ModernLibs.FileHelpers/Enums/FixedMode.cs |
| ATTRIBUTES-03 | [IgnoreFirst(N)] | Skips the first N input lines and captures them into `HeaderText`. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/IgnoreFirstAttribute.cs |
| ATTRIBUTES-04 | [IgnoreLast(N)] | Skips the last N input lines and captures them into `FooterText` (ring-buffered in async streaming). | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/IgnoreLastAttribute.cs |
| ATTRIBUTES-05 | [IgnoreEmptyLines] | Skips zero-length lines during parsing. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/IgnoreEmptyLinesAttribute.cs |
| ATTRIBUTES-06 | [IgnoreCommentedLines(c)] | Skips lines whose first character is the configured comment marker. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/IgnoreCommentedLinesAttribute.cs |
| ATTRIBUTES-07 | [ConditionalRecord(cond, pattern)] | Includes or excludes records by prefix / contains / ends / regex; regex is validated at compile time. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Records/ConditionalRecordAttribute.cs, src/ModernLibs.FileHelpers/Enums/RecordCondition.cs, src/ModernLibs.FileHelpers/Parsing/RecordConditionMatcher.cs |

---

## CONVERTERS

Span-based type conversion. Built-in `ISpanConverter<T>` implementations cover string, primitive, date/time, decimal, and enum types and are selected via `ConverterKind`. Custom converters plug in via `ISpanConverter<T>` directly (zero allocation) or the legacy `ConverterBase` adapter for FileHelpers 3.x parity.

### Built-in converters

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| CONVERTERS-01 | String / Char / Guid converters | Identity span and primitive parsers for string, single-character, and `Guid` fields. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/StringConverter.cs, src/ModernLibs.FileHelpers/Converters/CharConverter.cs, src/ModernLibs.FileHelpers/Converters/GuidConverter.cs |
| CONVERTERS-02 | Boolean converter | Accepts `true`/`false` plus `1`/`0` numeric forms. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/BoolConverter.cs |
| CONVERTERS-03 | Integer family converters | `Byte`, `SByte`, `Int16`/`Int32`/`Int64`, `UInt16`/`UInt32`/`UInt64` culture-invariant converters. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/IntegerConverters.cs |
| CONVERTERS-04 | Float family converters | `Decimal`, `Double`, `Single`, plus `PercentDouble` for `"42.5%"` inputs. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/FloatConverters.cs |
| CONVERTERS-05 | DateTime converters | Single-format and multi-format `DateTime` parsing per `ConverterKind.Date` / `DateMultiFormat`. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/DateTimeConverters.cs |
| CONVERTERS-06 | Enum converter | Name-based enum parsing for `[FieldConverter(typeof(MyEnum))]`. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/EnumConverter.cs |

### Custom converter API

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| CONVERTERS-07 | ISpanConverter<T> | Zero-allocation `TryParse` / `TryFormat` API for plug-in span converters. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/ISpanConverter.cs |
| CONVERTERS-08 | ConverterBase + adapter | Legacy reflection-free bridge so `[FieldConverter(typeof(X))]` can wrap a `ConverterBase` subclass into the `ISpanConverter<T>` pipeline. | — | active | lookup | src/ModernLibs.FileHelpers/Converters/ConverterBase.cs |

---

## ENGINES

Sync batch read/write engine targeting FileHelpers 3.x parity. `FileHelperEngine<T>` materialises the full input before parsing and is best for files that fit in memory; for streaming use `FileHelperAsyncEngine<T>` (see ASYNC). Header/footer round-trip, append mode, and configurable `Encoding` / `NewLineForWrite` are first-class.

### Batch read & write

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| ENGINES-01 | Read records from string | `FileHelperEngine<T>.ReadString` materialises the input and parses to `T[]`. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-02 | Read records from TextReader | `FileHelperEngine<T>.ReadStream` reads to end and parses; the caller owns the reader. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-03 | Read records from file | `FileHelperEngine<T>.ReadFile` opens a `StreamReader` with the configured `Encoding` and parses. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-04 | Cap reads at maxRecords | `ReadString` / `ReadStream` / `ReadFile` overloads stop after N successfully parsed records. | — | active | list | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-05 | Write records to string | `FileHelperEngine<T>.WriteString` returns a single string with optional header/footer framing. | — | active | report | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-06 | Write records to TextWriter | `FileHelperEngine<T>.WriteStream` emits records to a caller-owned writer. | — | active | report | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-07 | Write records to file | `FileHelperEngine<T>.WriteFile` creates or overwrites the target path using the configured `Encoding`. | — | active | report | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-08 | Append record(s) to file | `AppendToFile(path, T)` and `AppendToFile(path, IEnumerable<T>)` append in `FileMode.Append`; header/footer are not emitted. | — | active | crud | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| ENGINES-09 | Header/Footer/NewLine/Encoding round-trip | `HeaderText`, `FooterText`, `NewLineForWrite`, and `Encoding` properties round-trip framing across read and write. | — | active | navigation | src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |

---

## ERRORS

Per-call error handling. `ErrorMode` selects between throw, ignore-and-continue, and save-and-continue; `ErrorManager` collects `ErrorInfo` entries with line numbers, source line text, and exceptions, and can persist them to disk.

### Error capture & strategy

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| ERRORS-01 | ErrorMode strategy | Selects between `ThrowException`, `IgnoreAndContinue`, and `SaveAndContinue` when a record fails to parse. | — | active | validation | src/ModernLibs.FileHelpers/Enums/ErrorMode.cs, src/ModernLibs.FileHelpers/Errors/ErrorManager.cs |
| ERRORS-02 | ErrorManager capture | `Errors`, `ErrorCount`, `HasErrors`, and `ClearErrors` expose accumulated parse failures from the most recent call. | — | active | validation | src/ModernLibs.FileHelpers/Errors/ErrorManager.cs |
| ERRORS-03 | SaveErrors to file | `ErrorManager.SaveErrors(path[, encoding])` persists captured failures to a text file. | — | active | validation | src/ModernLibs.FileHelpers/Errors/ErrorManager.cs |
| ERRORS-04 | ErrorInfo record | Per-failure entry with `LineNumber`, `RecordString`, and `Exception` for diagnostics. | — | active | validation | src/ModernLibs.FileHelpers/Errors/ErrorInfo.cs |

---

## EVENTS

Per-record extensibility. Engines raise `BeforeReadRecord` / `AfterReadRecord` / `BeforeWriteRecord` / `AfterWriteRecord` / `Progress` events; record types can additionally implement `INotifyRead<T>` / `INotifyWrite<T>` for record-local logic, and both fire in tandem.

### Engine events

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| EVENTS-01 | BeforeReadRecord | Pre-parse hook with options to skip the record, replace the input line, or stop reading. | — | active | navigation | src/ModernLibs.FileHelpers/Events/BeforeReadEventArgs.cs, src/ModernLibs.FileHelpers/Events/EventHandlers.cs, src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| EVENTS-02 | AfterReadRecord | Post-parse hook with an option to skip the parsed record. | — | active | navigation | src/ModernLibs.FileHelpers/Events/AfterReadEventArgs.cs, src/ModernLibs.FileHelpers/Events/EventHandlers.cs, src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| EVENTS-03 | BeforeWriteRecord | Pre-format hook with an option to skip emission. | — | active | navigation | src/ModernLibs.FileHelpers/Events/BeforeWriteEventArgs.cs, src/ModernLibs.FileHelpers/Events/EventHandlers.cs, src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| EVENTS-04 | AfterWriteRecord | Post-format hook that can mutate the formatted line before it is written. | — | active | navigation | src/ModernLibs.FileHelpers/Events/AfterWriteEventArgs.cs, src/ModernLibs.FileHelpers/Events/EventHandlers.cs, src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |
| EVENTS-05 | Progress reporting | `ProgressChangeHandler` invoked per record yielded or written, suitable for progress UIs. | — | active | navigation | src/ModernLibs.FileHelpers/Events/ProgressEventArgs.cs, src/ModernLibs.FileHelpers/Events/EventHandlers.cs, src/ModernLibs.FileHelpers/Engines/FileHelperEngine.cs |

### Record interfaces

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| EVENTS-06 | INotifyRead<T> | Record types implement `BeforeRead` / `AfterRead` callbacks invoked alongside engine read events. | — | active | navigation | src/ModernLibs.FileHelpers/Events/INotifyRead.cs |
| EVENTS-07 | INotifyWrite<T> | Record types implement `BeforeWrite` / `AfterWrite` callbacks invoked alongside engine write events. | — | active | navigation | src/ModernLibs.FileHelpers/Events/INotifyWrite.cs |

---

## FIELDS

Field-level annotations. `layout` covers ordinal position, fixed-length width, alignment, trim, quoting, per-field delimiter, multi-line continuation, and whether the field participates in the file at all. `conversion` selects a converter and configures null-value substitution. `validation` enforces runtime checks that reject parsed records.

### Conversion

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| FIELDS-11 | [FieldConverter(kind or type, args)] | Selects a built-in converter via `ConverterKind` or a custom converter via `typeof(X)` plus optional format args. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldConverterAttribute.cs, src/ModernLibs.FileHelpers/Enums/ConverterKind.cs |
| FIELDS-12 | [FieldNullValue(value)] | Substitutes the supplied value when the source field is empty or null. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldNullValueAttribute.cs |

### Layout & format

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| FIELDS-01 | [FieldOrder(n)] | Sets the explicit ordinal position of a field in the file layout. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldOrderAttribute.cs |
| FIELDS-02 | [FieldFixedLength(n)] | Declares the column width in characters for a fixed-length field. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldFixedLengthAttribute.cs |
| FIELDS-03 | [FieldAlign(mode, char)] | Left/right/center pads fixed-length output with a configurable pad character. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldAlignAttribute.cs, src/ModernLibs.FileHelpers/Enums/AlignMode.cs |
| FIELDS-04 | [FieldTrim(mode)] | Trims leading and/or trailing whitespace from the source field on read. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldTrimAttribute.cs, src/ModernLibs.FileHelpers/Enums/TrimMode.cs |
| FIELDS-05 | [FieldQuoted(char, mode)] | Marks the field as quoted; multiline-aware when `DelimitedRecord.MultilineMode` allows it. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldQuotedAttribute.cs, src/ModernLibs.FileHelpers/Enums/QuoteMode.cs |
| FIELDS-06 | [FieldDelimiter(s)] | Per-field delimiter override for delimited records. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldDelimiterAttribute.cs |
| FIELDS-07 | [FieldInNewLine] | Field continues on the next physical line, supporting multi-line logical records. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldInNewLineAttribute.cs |
| FIELDS-08 | [FieldHidden] | Excludes the field from the file layout while keeping it on the record type. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldHiddenAttribute.cs |
| FIELDS-09 | [FieldNotInFile] | Declares a virtual field that is never present in the file (computed/default-only). | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldNotInFileAttribute.cs |
| FIELDS-10 | [FieldOptional] | Marks a trailing field as optional; missing trailing fields take their default value. | — | active | navigation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldOptionalAttribute.cs |

### Runtime validation

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| FIELDS-13 | [FieldNotEmpty] | Runtime validation: rejects records whose source field is empty. | — | active | validation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldNotEmptyAttribute.cs |
| FIELDS-14 | [FieldValueDiscarded] | Runtime validation: rejects records whose source field equals a configured sentinel value. | — | active | validation | src/ModernLibs.FileHelpers/Attributes/Fields/FieldValueDiscardedAttribute.cs |

---

## GENERATOR

`IIncrementalGenerator` pipeline. Discovers `[DelimitedRecord]` / `[FixedLengthRecord]` types via `ForAttributeWithMetadataName`, emits `IRecordDescriptor<T>` (and `IRecyclableDescriptor<T>` for class records), and registers them via `[ModuleInitializer]` into `FileHelperRegistry<T>`. v4-only — replaces the legacy `DYNAMIC` module whose `ClassBuilder` and `CsvEngine` relied on AOT-incompatible runtime reflection.

### Generator pipeline

| ID | Feature | Description | UI placement | Status | Type | Source |
|---|---|---|---|---|---|---|
| GENERATOR-01 | Discover record types | `ForAttributeWithMetadataName` arms for `[DelimitedRecord]` and `[FixedLengthRecord]` feed an equatable `RecordModel` pipeline that is deterministic and cacheable. | — | active | integration | src/ModernLibs.FileHelpers.Generator/FileHelpersGenerator.cs, src/ModernLibs.FileHelpers.Generator/Extraction/RecordModelExtractor.cs, src/ModernLibs.FileHelpers.Generator/Models/RecordModel.cs |
| GENERATOR-02 | Emit IRecordDescriptor<T> | `DescriptorEmitter` generates `Parse` and `Format` method bodies as `file`-scoped types; no reflection at runtime. | — | active | integration | src/ModernLibs.FileHelpers.Generator/Emission/DescriptorEmitter.cs, src/ModernLibs.FileHelpers/Descriptors/IRecordDescriptor.cs |
| GENERATOR-03 | Emit IRecyclableDescriptor<T> | Class records additionally receive a `ParseInto` entry point that fills an existing instance for zero-alloc recycling. | — | active | integration | src/ModernLibs.FileHelpers.Generator/Emission/DescriptorEmitter.cs, src/ModernLibs.FileHelpers/Descriptors/IRecyclableDescriptor.cs |
| GENERATOR-04 | Auto-register via [ModuleInitializer] | Generated `[ModuleInitializer]` populates `FileHelperRegistry<T>.Descriptor` before user code runs. | — | active | integration | src/ModernLibs.FileHelpers.Generator/Emission/DescriptorEmitter.cs, src/ModernLibs.FileHelpers/Registry/FileHelperRegistry.cs |
