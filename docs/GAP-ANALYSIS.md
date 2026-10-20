# Gap analysis: FileHelpers 3.x features that are not included

This page lists the FileHelpers 3.5.2 features that ModernLibs.FileHelpers 4.8.0 does not provide, and
why. It is a companion to the [migration guide](MIGRATION.md), which lists every API and behaviour
difference and what to use instead. Features not listed here or in the migration guide are available.

The gaps fall into three groups:

1. [Features that need run-time code generation or reflection](#1-left-out-run-time-reflection-or-code-generation).
   They conflict with the design (source generation, Native AOT) and will not be added.
2. [Separate concerns](#2-left-out-separate-concerns) that are better served by other libraries or a few
   lines of user code.
3. [Gaps that could be closed](#3-gaps-that-could-be-closed) without reflection. Most are open; a few
   were decided against.

## 1. Left out: run-time reflection or code generation

The record layout is turned into code by a source generator at compile time. Anything that creates or
changes record types at run time, or picks the record type from a `Type` object, does not fit that
model and does not work under Native AOT.

| FileHelpers 3.x | Reason | Instead |
|---|---|---|
| `FileHelpers.Dynamic`: `ClassBuilder`, `DelimitedClassBuilder`, `FixedLengthClassBuilder`, `CsvClassBuilder`, field builders, `SaveToBinaryFile` / `SaveToXml` / `SaveToSourceFile` | Compiles C# or VB source at run time. | Record types declared in source; the generator gives the same result at compile time. |
| `CsvEngine`, `CsvOptions` | Built on `ClassBuilder`. | A `[DelimitedRecord(",")]` record type. For CSV files without a known schema, use a CSV-first library. |
| `SmartFormatDetector`, `RecordFormatInfo`, `FormatHint` (`FileHelpers.Detection`) | Its output is a `ClassBuilder`. | Declare the layout. |
| Non-generic engines: `FileHelperEngine(Type)`, `FileHelperAsyncEngine(Type)`, `MasterDetailEngine(Type, Type)`, `MultiRecordEngine(params Type[])` | Need `MakeGenericType` and reflection over the record type. | Generic engines; `MultiRecordType.For<T>(selector)` for multi-record files. |
| `RecordTypeSelector` (returns a `Type`) | Run-time type dispatch. | `MultiRecordSelector` (returns `bool`) per record type. |
| `DelimitedFileEngine<T>`, `FixedFileEngine<T>`, `engine.Options` (changing delimiter, `FixedMode`, `IgnoreFirst`, ... at run time) | The layout is compiled into the generated parser. | One record type per layout. A future version could add engine-level overrides for a few options; see [3](#3-gaps-that-could-be-closed). |
| `CommonEngine.SortFileByField`, `CommonEngine.SortRecordsByField` | Sorts by field name through reflection. | Sort in your own code. |

## 2. Left out: separate concerns

These features are not tied to reflection. They were left out because they are thin wrappers, depend
on legacy .NET Framework APIs, or solve problems that LINQ or dedicated tools solve well.

| FileHelpers 3.x | Instead |
|---|---|
| `CommonEngine` (`ReadFile<T>`, `WriteFile<T>`, `MergeFiles`, `RemoveDuplicateRecords`, `ReadSortedFile`, `DataTable` helpers, ...) | The engine methods, plus LINQ (`Distinct`, `OrderBy`, `Concat`). |
| `FileTransformEngine<TSource, TDestination>`, `ITransformable<T>` | Read with one engine, project with `Select`, write with another. Use `ReadSync` / `ReadAsync` to stream. |
| `BigFileSorter`, `BigFileSorter<T>` | An external sort tool, or chunked read, sort and merge in your code. |
| `FileDiffEngine<T>` | LINQ `Except` / `Intersect` with an `IEqualityComparer<T>`. |
| `FileDataLink`, `GenericDataLink`, `DataStorage` and its subclasses (`FileStorage`, `SqlServerStorage`, `OleDbStorage`, `AccessStorage`, `GenericDatabaseStorage`, `ExcelStorageBase`) | Your data-access library of choice. |
| `ExcelNPOIStorage` (separate 3.x package) | An Excel library. |
| `ReadFileAsDT`, `ReadStringAsDT`, `ReadStreamAsDT` and other `DataTable` interop | Typed records. Build a `DataTable` yourself if you need one. |

## 3. Gaps that could be closed

These could be added without reflection. "Open" means no decision has been made; "Not planned" means
it was considered and rejected. Contributions are welcome: please open an issue first.

### Attributes and layout

| Gap | Status | Notes |
|---|---|---|
| `[FieldArrayLength]` (array fields) | Open | Needs generator support for repeated delimited values and per-element fixed widths. |
| Header line from `[FieldCaption]` (`GetFileHeader()`) | Open | `[FieldCaption]` is already read into the field metadata. |
| Culture parameter on `[DelimitedRecord]` / `[FixedLengthRecord]` | Open | Culture can be set per field with `CultureName`. |
| `[IgnoreCommentedLines]` with a multi-character marker | Open | The marker is one `char`; `anyPlace` works as in 3.x. |
| Non-string `[FieldConverter(typeof(X), args)]` arguments | Open | Arguments are passed to the converter constructor, but must be strings. |
| `RecordCondition.IncludeIfEnclosed` / `ExcludeIfEnclosed` | Open | A regex condition covers the same cases. |
| Engine-level overrides for record options (delimiter, `FixedMode`, `IgnoreFirst`) | Open | Would cover part of `DelimitedFileEngine<T>` / `FixedFileEngine<T>`. |

### Engines

| Gap | Status | Notes |
|---|---|---|
| `ReadFileAsList` / `ReadStringAsList` / `ReadStreamAsList` | Open | One-line wrappers around the array methods. |
| `WriteFile` / `WriteStream` / `WriteString` with `maxRecords` (all engines) | Open | `records.Take(n)` does the same. |
| `[IgnoreFirst]` / `[IgnoreLast]` in `MultiRecordEngine` | Open | 3.x applies the options of the first record type. |
| Record-by-record API on `MultiRecordEngine` (`BeginReadFile`, `ReadNext`, `WriteNext`, ...) | Open | `MultiRecordAsyncEngine.ReadAsync` covers streaming reads. |
| `AppendToFile` on `MultiRecordEngine` and `MasterDetailEngine` | Open | |
| `FileHelperAsyncEngine<T>.Flush()` and the `bufferSize` overloads | Open | |
| `FooterText` after a streaming read that stops early | Open | Needs the remaining input to be read to find the last lines. |
| `LastRecordValues`, `this[int]` / `this[string]` indexers, `WriteNextValues()` on `FileHelperAsyncEngine<T>` | Not planned | Untyped access to field values; work with the typed record. |
| `ErrorManager.LoadErrors`, `ErrorLimit`, `SaveErrors(file, header)` | Open | |

## 4. Beyond FileHelpers 3.x

ModernLibs.FileHelpers also adds features that 3.x does not have: source generation with compile-time
diagnostics ([reference](diagnostics.md)), Native AOT support, struct and positional records, allocation-free reads
of value-type struct records, more built-in field types, span and callback reads, `IAsyncEnumerable<T>`
streaming, parallel reads, `StopReading` in read events and span-based custom converters. See
[MIGRATION.md section 4](MIGRATION.md#4-what-is-new) and [OPT-IN-FEATURES.md](OPT-IN-FEATURES.md).
