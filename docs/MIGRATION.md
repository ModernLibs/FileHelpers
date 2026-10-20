# Migrating from FileHelpers 3.x

ModernLibs.FileHelpers has a FileHelpers 3.x-style API: the same attribute names, engine classes,
events, `ErrorMode` and exception types. Most code migrates by changing the namespace. This guide
lists every difference we know of. It was checked against FileHelpers 3.5.2 and ModernLibs.FileHelpers
4.8.0.

The guide has three parts:

1. [Package and namespaces](#1-package-and-namespaces): the change every project needs.
2. [API differences](#2-api-differences): things that no longer compile, and what to use instead.
3. [Behaviour differences](#3-behaviour-differences): code that compiles but behaves differently.

The last sections cover [what is new](#4-what-is-new) and [troubleshooting](#5-troubleshooting).

Behaviour that is not listed here follows FileHelpers 3.x. If you find a difference that is not
listed, please [open an issue](https://github.com/ModernLibs/FileHelpers/issues).

## 1. Package and namespaces

Replace the package:

```xml
<!-- <PackageReference Include="FileHelpers" Version="3.5.2" /> -->
<PackageReference Include="ModernLibs.FileHelpers" Version="4.8.0" />
```

The library targets .NET 10 only and requires C# 11 or later (`MLFH026`; the .NET 10 default is
C# 14). Record types are processed by a source generator that ships in the package, so there is
nothing else to install.

All public types are in two namespaces:

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `FileHelpers` | `ModernLibs.FileHelpers` |
| `FileHelpers.Events` (event args, delegates, `INotifyRead`, `INotifyWrite`) | `ModernLibs.FileHelpers` |
| `FileHelpers.MasterDetail` (`MasterDetailEngine`, `MasterDetails`, `RecordAction`, `CommonSelector`) | `ModernLibs.FileHelpers` |
| `ConverterBase` (was in `FileHelpers`) | `ModernLibs.FileHelpers.Converters` |

```diff
- using FileHelpers;
- using FileHelpers.Events;
- using FileHelpers.MasterDetail;
+ using ModernLibs.FileHelpers;
+ using ModernLibs.FileHelpers.Converters;   // only if you write custom converters
```

Each record type is checked at compile time. A layout mistake that FileHelpers 3.x reported at run
time (for example a missing `[FieldFixedLength]` or an unsupported field type) is now a build error
or warning; see the [diagnostics reference](diagnostics.md) (`MLFH001`-`MLFH029`). `MLFH` warnings can be
suppressed as usual; errors that block the descriptor cannot (`#pragma`, `.editorconfig`, `NoWarn` are
ignored). If they are hidden with `[SuppressMessage]` or `RunAnalyzers=false`, using the record throws
a `BadUsageException` that names the errors (see
[suppressing diagnostics](diagnostics.md#suppressing-diagnostics)).

## 2. API differences

### 2.1 Types that are not available

| FileHelpers 3.x | Why | Use instead |
|---|---|---|
| `FileHelperEngine(Type)`, `FileHelperAsyncEngine(Type)`, `MasterDetailEngine(Type, Type)` (non-generic engines) | Need run-time reflection. | The generic engines, for example `new FileHelperEngine<Customer>()`. |
| `DelimitedFileEngine<T>`, `FixedFileEngine<T>`, `engine.Options` (`DelimitedRecordOptions`, `FixedRecordOptions`, `RecordOptions`) | The layout is compiled into generated code and cannot be changed at run time. | Declare one record type per layout. |
| `ClassBuilder`, `DelimitedClassBuilder`, `FixedLengthClassBuilder`, `CsvClassBuilder` and the rest of `FileHelpers.Dynamic` | Compile code at run time. | Record types declared in source. |
| `CsvEngine`, `CsvOptions` | Built on `ClassBuilder`. | A record type with `[DelimitedRecord(",")]`, or a CSV-first library if you have no fixed schema. |
| `SmartFormatDetector` and `FileHelpers.Detection` | Produces `ClassBuilder` output. | Declare the layout. |
| `RecordTypeSelector`, `MultiRecordEngine(RecordTypeSelector, params Type[])` | Run-time type dispatch. | `MultiRecordEngine` with `MultiRecordType.For<T>(selector)`, see [2.6](#26-multi-record-files). |
| `CommonEngine` | Static convenience wrappers. | The engine methods, for example `new FileHelperEngine<T>().ReadFile(path)`. |
| `FileTransformEngine`, `ITransformable<T>` | Not included. | Read with one engine, project with LINQ, write with another. |
| `FileDiffEngine`, `BigFileSorter` | Not included. | LINQ set operations; an external sort tool. |
| `FileDataLink`, `GenericDataLink`, the `DataStorage` classes (`FileStorage`, `SqlServerStorage`, `OleDbStorage`, ...), `ExcelNPOIStorage` | Not included (.NET Framework / extra dependencies). | Your data-access library of choice. |
| `[FieldArrayLength]` | Not implemented. | Read the values as separate fields or as one string field and split it. |
| `[FieldIgnored]` (obsolete in 3.x) | Removed. | `[FieldHidden]`. |
| Non-generic `INotifyRead` / `INotifyWrite` | Replaced. | `INotifyRead<T>` / `INotifyWrite<T>`, see [2.4](#24-events-and-record-notifications). |

[GAP-ANALYSIS.md](GAP-ANALYSIS.md) explains why each of these was left out.

### 2.2 `FileHelperEngine<T>`

The read, write and append methods of 3.x are available with the same names and parameters
(`ReadFile`, `ReadString`, `ReadStream`, the `maxRecords` overloads, `WriteFile`, `WriteString`,
`WriteStream`, `AppendToFile`), as are `HeaderText`, `FooterText`, `Encoding`, `NewLineForWrite`,
`ErrorMode`, `ErrorManager`, `LineNumber`, `TotalRecords` and the four record events.

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `ReadFileAsList`, `ReadStringAsList`, `ReadStreamAsList` | Not available. Use `new List<T>(engine.ReadFile(path))` or the `IEnumerable<T>` returned by `ReadSync`. |
| `ReadFileAsDT`, `ReadStringAsDT`, `ReadStreamAsDT` (`DataTable`) | Not available. |
| `WriteFile` / `WriteStream` / `WriteString` with a `maxRecords` argument | Not available. Pass `records.Take(n)`. |
| `GetFileHeader()` | Not available. `[FieldCaption]` is accepted and stored in the field metadata, but no header line is generated from it. Set `HeaderText` yourself. |
| `Options` (run-time layout changes) | Not available, see [2.1](#21-types-that-are-not-available). |
| `RecordType` | Not available. You already know `typeof(T)`. |
| `where T : class` | No constraint: structs and `record struct` types work too. |

`IFileHelperEngine<T>` and `IFileHelperAsyncEngine<T>` exist as in 3.x, with the members listed in this
section and the next.

### 2.3 `FileHelperAsyncEngine<T>`

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `IDisposable BeginReadFile(...)`, `BeginReadStream`, `BeginReadString`, `BeginWriteFile`, `BeginWriteStream` return `IDisposable` | They return `void`. Dispose the engine instead (`using var engine = ...`), or call `Close()`. |
| `BeginReadFile(fileName, bufferSize)`, `BeginWriteFile(fileName, bufferSize)` | Not available. |
| `BeginAppendToFile(fileName)` | `BeginAppendFile(filePath)` |
| `ReadNexts(int)` | `ReadNext(int)` |
| `ReadToEnd()` | `ReadRemaining()` |
| `WriteNexts(IEnumerable<T>)` | `WriteNext(IEnumerable<T>)` |
| `Flush()` | Not available. `Close()` / `Dispose()` flush the output. |
| `LastRecordValues`, `this[int]`, `this[string]`, `WriteNextValues()` | Not available. Work with the typed record. |
| `FileHelperAsyncEngine(Encoding)` constructor | Not available. Set the `Encoding` property. |
| One instance can run a `ReadAsync` enumeration next to a `BeginRead*` session | Not allowed: `ReadAsync`, `BeginRead*` and `BeginWrite*` throw `InvalidOperationException` while another read or write session is open on the same instance (they share `LineNumber`, `TotalRecords`, `HeaderText` and `ErrorManager`). Use a separate engine instance for each concurrent read or write. As in 3.x, reading while writing is rejected. |

```csharp
// 3.x:  using (engine.BeginReadFile("orders.txt")) { ... }
using var engine = new FileHelperAsyncEngine<Order>();
engine.BeginReadFile("orders.txt");

Order[] firstTen = engine.ReadNext(10);   // was ReadNexts(10)
Order[] rest = engine.ReadRemaining();    // was ReadToEnd()
```

### 2.4 Events and record notifications

The event names are the same, and the read event arguments keep `RecordLine`, `RecordLineChanged`,
`SkipThisRecord`, `LineNumber` and (after read) `Record`. The differences:

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| Handler signature `(EngineBase engine, XxxEventArgs<T> e)` | `(object sender, XxxEventArgs<T> e)`. Lambdas compile unchanged; named handler methods need the first parameter changed to `object`. |
| `e.Engine` | Not available. Use `sender`. |
| `BeforeWriteEventArgs<T>.LineNumber`, `AfterWriteEventArgs<T>.LineNumber` | `RecordNumber`. |
| `AfterReadEventArgs<T>.Record` is settable | Read-only. Change the record's members instead of replacing the record. |
| `BeforeReadEventArgs<T>.Record` | Not available (the record has not been parsed yet). |
| `event EventHandler<ProgressEventArgs> Progress` | `event ProgressChangeHandler Progress` with the same `(sender, ProgressEventArgs)` shape. Lambdas compile unchanged. |
| `INotifyRead` / `INotifyWrite` with non-generic event args | `INotifyRead<T>` / `INotifyWrite<T>` with generic event args. |

```csharp
[DelimitedRecord(",")]
public sealed class Order : INotifyRead<Order>   // 3.x: INotifyRead
{
    public int Id;
    public decimal Amount;

    public void BeforeRead(BeforeReadEventArgs<Order> e)   // 3.x: BeforeReadEventArgs
    {
        if (e.RecordLine.Contains("INVALID"))
            e.SkipThisRecord = true;
    }

    public void AfterRead(AfterReadEventArgs<Order> e) { }
}
```

As in 3.x, `BeforeRead` runs on the instance that then receives the field values, so state you set
there is kept, for class records with settable members. Struct records, records built through a
constructor and records with `init`-only members cannot be filled after creation: for them
`BeforeRead` runs on a separate default instance that is discarded, so use it only to inspect or skip
the line (`RecordLine`, `SkipThisRecord`).

### 2.5 Attributes

Attribute names and the common constructors are unchanged. These signatures differ:

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `[DelimitedRecord(delimiter, defaultCultureName)]`, `[FixedLengthRecord(fixedMode, defaultCultureName)]` | No culture parameter. Set the culture per field: `[FieldConverter(ConverterKind.Decimal, CultureName = "de-DE")]`. |
| `[IgnoreCommentedLines("#")]`, `[IgnoreCommentedLines("//", anyPlace)]` (string marker) | `[IgnoreCommentedLines('#')]`, `[IgnoreCommentedLines('#', anyPlace)]`: the marker is a single `char`. `anyPlace` works as in 3.x (default `true`: blanks may precede the marker). |
| `[FieldTrim(TrimMode, string trimChars)]` | `[FieldTrim(TrimMode, params char[] chars)]`, for example `[FieldTrim(TrimMode.Both, ' ', '*')]`. As in 3.x, the characters are skipped only around the quotes of a `[FieldQuoted]` field; the value itself is trimmed of white space. |
| `[FieldNullValue(typeof(DateTime), "2000-01-01")]` | Not available. Use a constant of the field type, for example `[FieldNullValue(-1)]` on an `int` field or `[FieldNullValue(0.0)]` on a `double` field. A value that does not fit the field type is reported as `MLFH010`. A `DateTime` value cannot be an attribute constant: declare the field as `DateTime?` (an empty field reads as `null`) and substitute the default in your code. |
| `[FieldConverter(typeof(MyConverter), params object[] args)]` | `[FieldConverter(typeof(MyConverter), params string[] args)]`: the arguments must be strings. See [3.8](#38-custom-converters). |
| `RecordCondition.IncludeIfMatchRegex`, `RecordCondition.ExcludeIfMatchRegex` | `RecordCondition.IncludeIfRegex`, `RecordCondition.ExcludeIfRegex` |
| `RecordCondition.IncludeIfEnclosed`, `RecordCondition.ExcludeIfEnclosed` | Not available. Use a regex condition. |

`[FieldDelimiter]` works as in 3.x: it sets the delimiter that *follows* its field. With
`[DelimitedRecord(",")]`, `{ A; [FieldDelimiter("|")] B; C }` reads and writes `a,b|c`; on the last
field the attribute only matters for the extra-delimiter check and `OptionalForWrite` quoting. 3.x
code needs no change. Code written for an earlier 4.x milestone, which used the attribute as the
delimiter *before* its field (`a|b,c`), must move the attribute one field earlier to keep its file
layout; files in the milestone layout are rejected with a field-count error ("Delimiter '|' not found
after field 'B'") instead of being misread.

`[FieldQuoted]` has all the 3.x constructors, including the `MultilineMode` overloads.
`[FieldFixedLength(n)]` keeps its `OverflowMode` property, and `AlignMode`, `FixedMode`, `QuoteMode`,
`MultilineMode`, `TrimMode` and `ConverterKind` keep their member names. `ConverterKind` has one new
member, `Enum`. `[DelimitedRecord]` has a new `MultilineMode` property; when you set it, it overrides
the multiline setting of every `[FieldQuoted]` field in the record.

Enum *numeric values* are not always the same as in 3.x (`ErrorMode`, `RecordAction`, `ConverterKind`).
This only matters if you store or cast the numbers; use the names.

### 2.6 Multi-record files

3.x chose the record type with a `RecordTypeSelector` that returned a `Type`. ModernLibs uses a
closed list of record types, each with a selector that returns `true` when a line belongs to it:

```csharp
var engine = new MultiRecordEngine(
    MultiRecordType.For<Order>(line => line.StartsWith("ORD")),
    MultiRecordType.For<OrderLine>(line => line.StartsWith("LIN")));

object[] records = engine.ReadFile("mixed.txt");
foreach (object record in records)
{
    if (record is Order order) { /* ... */ }
}
```

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `MultiRecordEngine(RecordTypeSelector, params Type[])`, `RecordSelector` property | `MultiRecordEngine(params IMultiRecordType[])` with `MultiRecordType.For<T>(MultiRecordSelector)` |
| Selector receives a `string` | Selector receives a `ReadOnlySpan<char>`. `StartsWith`, `Contains`, `EndsWith`, `Length` and indexing work; call `line.ToString()` if you need `string`-only methods. |
| Record-by-record API on `MultiRecordEngine` (`BeginReadFile`, `ReadNext`, `BeginWriteFile`, `WriteNext`, `Close`) | Not available. Use `MultiRecordAsyncEngine.ReadAsync` for streaming reads. |
| `AppendToFile`, `WriteFile` / `WriteStream` / `WriteString` with `maxRecords` | Not available. |

### 2.7 Master/detail files

```csharp
// 3.x enum CommonSelector -> CommonSelectorMode
var engine = new MasterDetailEngine<Order, OrderLine>(CommonSelectorMode.MasterIfBegins, "ORD");

// or a selector of your own
var custom = new MasterDetailEngine<Order, OrderLine>(line =>
    line.StartsWith("ORD") ? RecordAction.Master :
    line.StartsWith("LIN") ? RecordAction.Detail :
    RecordAction.Skip);

MasterDetails<Order, OrderLine>[] groups = engine.ReadFile("orders.txt");
foreach (var group in groups)
    Console.WriteLine($"{group.Master.Id}: {group.Details.Count} lines");
```

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `enum CommonSelector` (`MasterIfBegins`, ...) | `enum CommonSelectorMode` with the same members. The name `CommonSelector` is now a static class of selector presets (`CommonSelector.BeginsWith("ORD")`, `Contains`, `EndsWith`, `Equals`, `HasLengthLessThan`, `HasLengthMoreThan`). |
| `MasterDetailSelector(string recordString)` | `MasterDetailSelector(ReadOnlySpan<char> line)`, see the selector note in [2.6](#26-multi-record-files). |
| Parameterless constructor plus `RecordSelector` property | Pass the selector to the constructor. |
| `MasterDetails<M, D>.Details` is `D[]`; `Master` and `Details` are settable; parameterless constructor | `Details` is `IReadOnlyList<TDetail>` (use `Count`, not `Length`); both properties are read-only; construct with `new MasterDetails<M, D>(master, details)`. |
| `AppendToFile`, `WriteFile` / `WriteStream` / `WriteString` with `maxRecords` | Not available. |

### 2.8 Errors

The exception types have the same names and hierarchy as 3.x: `FileHelpersException` is the base of
`BadUsageException`, `NullValueNotFoundException` (derives from `BadUsageException`) and
`ConvertException`. `ConvertException` has the 3.x properties (`FieldName`, `FieldType`,
`FieldStringValue`, `LineNumber`, `ColumnNumber`, `MessageExtra`, `MessageOriginal`).

| FileHelpers 3.x | ModernLibs.FileHelpers |
|---|---|
| `ErrorManager.Errors` is `ErrorInfo[]` | `IReadOnlyList<ErrorInfo>`: use `Count` instead of `Length`. |
| `ErrorManager` implements `IEnumerable` | It does not; iterate `ErrorManager.Errors`. |
| `SaveErrors(fileName, string header)` | `SaveErrors(filePath, Encoding encoding)`. `SaveErrors(filePath)` is unchanged. The file format is a tab-separated text report, not the 3.x format; its column numbers are 1-based, like `ConvertException.ColumnNumber`. |
| `ErrorManager.LoadErrors(fileName)` | Not available. |
| `ErrorLimit` (default 10 000) | Not available; all errors are kept. |
| `new ErrorManager(ErrorMode)` | Use the parameterless constructor and set `ErrorMode`. |
| `ErrorInfo.RecordTypeName` | Not available. `ErrorInfo` adds `FieldName`, `ColumnIndex` and `RawFieldValue`. |

### 2.9 Converters

`ConverterBase` keeps its 3.x members (`StringToField`, `FieldToString`, `CustomNullHandling`,
`ThrowConvertException`, `DefaultDateTimeFormat`). It now lives in `ModernLibs.FileHelpers.Converters`.
The built-in converters (`DecimalConverter`, `DateTimeConverter`, ...) are public classes in the same
namespace, which they were not in 3.x.

### 2.10 Record layout rules

These rules are checked when you build, not when the engine is created. Apart from the members of
the first rule that cannot be mapped, they are the same as in 3.x.

- **Which members are mapped.** 3.x maps every instance field (any accessibility, including the
  backing fields of auto-properties) and no other properties. Here `public`, `internal` and
  `protected internal` fields and properties are mapped (a property needs an accessible getter, and an
  accessible setter, an `init` accessor or a matching constructor parameter). `private`, `protected` and
  `private protected` members (and properties without an accessible getter) cannot be reached by
  generated code and are not part of the layout: an annotated one is an error (`MLFH023`), an
  unannotated one that 3.x would map is a warning (`MLFH028`), so a migrated record cannot lose a
  column silently. Make such a member `public` or `internal` to keep the column, or mark it
  `[FieldHidden]` if it is not one. Properties with explicit accessors, which 3.x ignores, are now
  columns when they meet these conditions; mark them `[FieldHidden]` to keep them out.
- **`[FieldOrder]` on all fields or none.** If one mapped field has `[FieldOrder]`, every mapped field
  needs it (`MLFH024`); `[FieldHidden]` members are exempt. 3.x threw a `BadUsageException`.
- **`[FieldQuoted]` only in delimited records.** On a field of a fixed-length record it is an error
  (`MLFH025`), as 3.x threw a `BadUsageException`.
- **`[IgnoreInheritedClass]`** stops the walk up the inheritance chain at the class that carries it,
  as in 3.x: for `C : B : A` with the attribute on `B`, the fields of `B` and `C` are mapped, not those
  of `A`.
- `ref struct` types cannot be record types (`MLFH027`).

```csharp
[DelimitedRecord(",")]
public sealed class Invoice
{
    public int Number;
    internal decimal Total;               // mapped, as in 3.x
    [FieldHidden] internal string? Note;  // left out of the layout
    private int _retries;                 // MLFH028: a column in 3.x, not mapped here
}
```

## 3. Behaviour differences

Since 4.8.0, quoted fields (`QuoteMode`, multiline values, blanks before quotes), `[FieldTrim]`
characters, comment lines (`anyPlace`), `[FieldInNewLine]` and custom converter arguments behave as in
3.x. The remaining differences are:

### 3.1 Default encoding is UTF-8

3.x used `Encoding.GetEncoding(0)`. On .NET Framework that is the system ANSI code page (for example
Windows-1252); on .NET Core and later it already is UTF-8. ModernLibs uses `Encoding.UTF8` on every
platform, which also writes a byte order mark when it creates a file. If your files use a legacy code
page, set it explicitly:

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);   // needed on .NET for code pages
var engine = new FileHelperEngine<Customer>(Encoding.GetEncoding(1252));
```

### 3.2 Line endings and empty header/footer

- `NewLineForWrite` defaults to `"\r\n"` on every platform. 3.x used `Environment.NewLine`, which is
  `"\n"` on Linux and macOS. Set `engine.NewLineForWrite = "\n";` if you need that. This applies to
  `MultiRecordEngine` and `MasterDetailEngine` too: they have a `NewLineForWrite` property with the
  same default, which also sets the line break inside `[FieldInNewLine]` records.
- `HeaderText` and `FooterText` are `null` when there is no header or footer. In 3.x they were empty
  strings, so code like `engine.HeaderText.Length` needs a null check.

### 3.3 Empty fields and conversion errors

These follow 3.x since 4.8.0 (earlier 4.x milestones were different):

- An empty field for a non-nullable value type raises `NullValueNotFoundException` unless the field has
  `[FieldNullValue]`. Nullable value types (`int?`) become `null`; strings keep their text.
- A value that cannot be converted raises `ConvertException`; it is never replaced by `default`.
- `bool` accepts `true`/`false`, `1`/`0`, `y`/`n`, `t`/`f` (trimmed, case-insensitive), as in 3.x. An
  empty `bool` field follows the empty-field rule above. `yes`/`no` are not accepted by default
  (earlier 4.x milestones accepted them); declare the strings as in 3.x:
  `[FieldConverter(ConverterKind.Boolean, "yes", "no")]`.
- In `ErrorMode.ThrowException`, library exceptions are thrown as-is; any other exception from the
  read is wrapped in a `FileHelpersException`.

### 3.4 Dates and percentages

- `DateTime` fields without a format use `ConverterBase.DefaultDateTimeFormat`, which is `ddMMyyyy`
  as in 3.x, for reading and writing.
- `DateOnly` and `TimeOnly` (not in 3.x) default to `MM/dd/yyyy` and `HH:mm:ss`, which is not the
  same convention as `DateTime`. `ConverterKind.Date` only applies to `DateTime` fields; for another
  format, wrap the built-in converter in a custom converter:

  ```csharp
  public sealed class IsoDateOnlyConverter : ISpanConverter<DateOnly>
  {
      private static readonly DateOnlyConverter Inner = new("yyyy-MM-dd");

      public bool TryParse(ReadOnlySpan<char> span, out DateOnly value) => Inner.TryParse(span, out value);

      public bool TryFormat(DateOnly value, Span<char> destination, out int charsWritten) =>
          Inner.TryFormat(value, destination, out charsWritten);
  }

  [DelimitedRecord(",")]
  public sealed class Shipment
  {
      [FieldConverter(typeof(IsoDateOnlyConverter))]
      public DateOnly ShippedOn;
  }
  ```

- `ConverterKind.PercentDouble` works as in FileHelpers 3.5.2, not as the 3.x documentation describes
  it. It reads and writes plain numbers (`50` reads as `50`, `0.5` is written as `0.5`), and a value
  that contains `%` raises `ConvertException`. The 3.x documentation says `50%` reads as `0.5`, but
  3.x itself rejects any value with `%`. For files that contain `%`, use a custom converter.

### 3.5 `[FieldInNewLine]` records

`[FieldInNewLine]` follows 3.x. When writing, the field starts on a new line. In a delimited record the
delimiter stays at the end of the previous line, and the line break is the engine's `NewLineForWrite`:

```csharp
[DelimitedRecord(",")]
public sealed class Host
{
    public int Id;
    [FieldQuoted('"', QuoteMode.OptionalForBoth)] public string Name = "";
    [FieldInNewLine] public string Ip = "";
    public string Note = "";
}
```

```
107,"a,b",
166.90.252.2,5" ruler
```

When reading, each physical line is checked against its own fields, and each field keeps its own
quote rules (here `"a,b"` is a `[FieldQuoted]` value and `5" ruler` is plain text). Quoted values
may span lines inside such a record, and `[FieldDelimiter]` works with it (`a,b|` followed by `c,d`
on the next line). As in 3.x, a line before a new-line field must end with the delimiter that follows
its last field (`107,abc,`, not `107,abc`), unless the new-line field is `[FieldOptional]` or the line
ends with a quoted value (`107,"a,b"` is accepted); without it the line is an error ("Delimiter ','
not found after field 'Name'"). Empty, comment and
`[ConditionalRecord]`-excluded lines between records are skipped on their own. An error is reported
on the physical line that fails, and after a layout error the following lines are read as the start of
the next record, as in 3.x (in `MultiRecordEngine` and `MasterDetailEngine` they go through the
selector again).

One difference from 3.x remains: a conversion error skips the whole record; 3.x continues with the
next line.

A `[FieldInNewLine]` first field behaves as in 3.x: every record begins with a blank line. Combined
with `[IgnoreEmptyLines]` that blank line is skipped and the record cannot be read (3.x behaves the
same); the build reports this as `MLFH029`.

### 3.6 Multi-record files

- `[IgnoreFirst]` and `[IgnoreLast]` are not applied by `MultiRecordEngine` (3.x applied the ones on
  the first record type). Header and footer lines are still dropped as long as no selector matches
  them, because lines that match no selector are skipped.
- Skipping unmatched lines is the 3.x behaviour (earlier 4.x milestones raised an error).

### 3.7 Footer text in streaming reads

`FooterText` (from `[IgnoreLast]`) is set when a read reaches the end of the input. If you stop a
streaming read early, for example by calling `Close()` on `FileHelperAsyncEngine<T>` before the last
record or by leaving a `ReadSync` / `ReadAsync` loop, `FooterText` stays `null`.

### 3.8 Custom converters

Custom converters work as in 3.x. `[FieldConverter(typeof(MyConverter))]` needs a public parameterless
constructor (`MLFH006` otherwise). `[FieldConverter(typeof(MyConverter), "a", "b")]` passes the
arguments to a public constructor that takes that many `string` parameters, or a `params string[]`
constructor (`MLFH022` otherwise). The generated code calls the constructor directly, so there is no
reflection:

```csharp
public sealed class TextBoolConverter : ConverterBase
{
    private readonly string _trueText;
    private readonly string _falseText;

    public TextBoolConverter(string trueText, string falseText)
    {
        _trueText = trueText;
        _falseText = falseText;
    }

    public override object? StringToField(string from) => from == _trueText;

    public override string? FieldToString(object? from) => from is true ? _trueText : _falseText;
}

[DelimitedRecord(",")]
public sealed class Flags
{
    [FieldConverter(typeof(TextBoolConverter), "ja", "nein")]
    public bool Active;
}
```

Unlike 3.x, the arguments must be strings (3.x accepted `params object[]`).

As in 3.x, a `ConverterBase` is not called for empty or blank input unless it overrides
`CustomNullHandling` to return `true`; the empty value is then handled as described in
[3.3](#33-empty-fields-and-conversion-errors). Arguments to the built-in converters
(`[FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]`, decimal separators, culture names) work as in
3.x.

### 3.9 Struct records and events

3.x engines and events required `T : class`. Struct records work here, but `e.Record` in an event is a
copy of a struct record, so an `AfterReadRecord` handler cannot change a struct record (it can change a
class record's members). `RecycleRecords` has no effect for structs.

## 4. What is new

None of this is needed to migrate, but it is available once you have:

- **Source generation and AOT.** No reflection at run time; works with `PublishAot=true` and trimming.
  Layout errors are reported at compile time ([diagnostics reference](diagnostics.md)).
- **Structs and records.** `struct`, `record struct`, positional records, classes without a
  parameterless constructor, and C# 11 `required` members. Value-type-only structs are read without per-record allocations through
  callbacks and `ReadSync`.
- **More field types.** `DateOnly`, `TimeOnly`, `DateTimeOffset`, `TimeSpan`, nullable value types and
  enums without a custom converter.
- **Span input and callbacks.** `ReadSpan(ReadOnlySpan<char>)`, callback overloads of `ReadFile`,
  `ReadString` and `ReadSpan`, and by-reference callbacks (`SpanRecordCallback<T>`).
- **Streaming.** `ReadSync` (`IEnumerable<T>`), `ReadAsync` (`IAsyncEnumerable<T>`) on both engines,
  `ReadFileAsync` with a callback, and `MultiRecordAsyncEngine` / `MasterDetailAsyncEngine`.
- **Parallel reads.** `ParallelReadFile` / `ParallelReadString` with sequential semantics.
- **Record-level multiline switch.** `[DelimitedRecord(",", MultilineMode = ...)]` sets the multiline
  mode of every quoted field at once.
- **`BeforeReadEventArgs<T>.StopReading`** to end a read from an event handler.
- **`ISpanConverter<T>`** for custom converters that work on spans without allocating strings.
- **Error details.** `ErrorInfo.FieldName`, `ColumnIndex` and `RawFieldValue`.

See [OPT-IN-FEATURES.md](OPT-IN-FEATURES.md) for examples.

## 5. Troubleshooting

**`InvalidOperationException: No IRecordDescriptor<T> registered`.** The record type was not processed
by the source generator; the message lists the likely causes. Check that it has `[DelimitedRecord]` or
`[FixedLengthRecord]`, that the build shows no `MLFH` errors for it (with `RunAnalyzers=false` they are
not reported; `MLFH017` and `MLFH026` are the usual ones here), and that the project references the
`ModernLibs.FileHelpers` package (a project reference to the library source must also reference the
generator as an analyzer).
Records declared by another source generator are not seen by the FileHelpers generator; declare them
in your own source files.

**`BadUsageException`: "No descriptor was generated for '...' because the record has FileHelpers
errors, which were hidden from the build".** `MLFH` errors that block the descriptor were hidden with
`[SuppressMessage]` or `RunAnalyzers=false`. The message lists the errors; fix them (see
[suppressing diagnostics](diagnostics.md#suppressing-diagnostics)).

**Columns are shifted, or a field you did not expect is read.** An `internal` (or
`protected internal`) member is mapped, as in 3.x. Mark members that are not in the file
`[FieldHidden]`; see [2.10](#210-record-layout-rules).

**`MLFH028` on a `private` or `protected` field.** 3.x reads it as a column, but generated code
cannot reach it, so it is not part of the layout. Make it `public` or `internal` if it is a column,
or mark it `[FieldHidden]`; see [2.10](#210-record-layout-rules).

**A file written by an earlier 4.x milestone fails with "Delimiter '...' not found after field".**
The record uses `[FieldDelimiter]`, which now follows its field as in 3.x. Move the attribute one
field earlier; see [2.5](#25-attributes).

**`MLFH023`, `MLFH024` or `MLFH025` on code that worked with an earlier 4.x milestone.** A `private`
or `protected` member carries a field attribute, only some fields have `[FieldOrder]`, or a
fixed-length field has `[FieldQuoted]`. 3.x rejected these layouts too; see
[2.10](#210-record-layout-rules).

**`MLFH026` on every record type.** The project compiles with a C# version below 11. Remove the
`<LangVersion>` setting (the .NET 10 default is C# 14) or set it to 11 or later.

**`InvalidOperationException` from `FileHelperAsyncEngine<T>`** ("A BeginRead* session is open on
this engine ..."). One engine instance runs one read or write session at a time. Call `Close()`
first, or use a separate instance for the second read.

**`MLFH013` unsupported field type.** The field type has no built-in conversion. Add a custom converter
with `[FieldConverter(typeof(...))]`.

**`MLFH006` or `MLFH022` on a custom converter.** The converter has no public constructor that matches
the `[FieldConverter]` arguments: parameterless when there are none, otherwise one `string` parameter
per argument (or `params string[]`). See [3.8](#38-custom-converters).

**A record type compiled with 3.x but reports `MLFH014` or `MLFH015`.** The generator could not find a
way to create the record or assign a member. Records need a public or internal parameterless
constructor, or a constructor whose parameter names match the fields; annotated members must be
settable.

**`BadUsageException` about a quote** ("does not begin with the quote char", "unclosed quoted string",
spaces before the quote char). The input does not match the field's `[FieldQuoted]` settings, exactly
as in 3.x: use `QuoteMode.OptionalForRead` to allow unquoted values, `[FieldTrim(TrimMode.Both)]` to
allow blanks around the quotes, or a `MultilineMode` that allows reading for values with line breaks.

For every `MLFH` rule, see the [diagnostics reference](diagnostics.md).
