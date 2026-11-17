# Generated-code contract

This page is for maintainers. The source generator writes a descriptor class into every consumer assembly
that has a `[DelimitedRecord]` or `[FixedLengthRecord]` type. That code is compiled against the library
version the consumer built with, and later runs against whatever library version the application loads.
A library built against 4.8 or 4.9 must keep working with every later 4.x runtime.

So the members below are a public contract, even where they are hidden from IntelliSense
(`[EditorBrowsable(Never)]`). Their signatures and their behaviour must not change in 4.x.

- Source: `src/ModernLibs.FileHelpers.Generator/Emission/DescriptorEmitter.cs` at `3a045e0` (tag `v4.9.1`).
  The 4.8.0 descriptors in `tests/ModernLibs.FileHelpers.Tests/Compat/Generated48/` bind to the same set.
  The 4.9.1 descriptors in `tests/ModernLibs.FileHelpers.Tests/Compat/Generated49/` are the output of that
  emitter for 19 record shapes.
- "Bound at" gives the line in `DescriptorEmitter.cs` (`Emitter:`) that emits the reference. Every line number
  was checked against the file at `3a045e0`.
- Members of the .NET base library that generated code uses (`MemoryExtensions`, `ArrayPool<char>`, `Unsafe`,
  `MemoryMarshal`, `CultureInfo.GetCultureInfo`, `RuntimeHelpers.GetUninitializedObject`) are not listed.

## Members

| Member | Bound at |
|---|---|
| `IRecordDescriptor<T>`: `Parse(ReadOnlySpan<char>, int)`, default `Parse(ReadOnlySpan<char>)`, `Format(T, IBufferWriter<char>)`, `Fields`, `Options`, `CreateDefaultInstance()` | `Emitter:61` (the interface), `:487` (`Parse`), `:1307` (`Format`), `:90` (`Fields`), `:1408` (`Options`), `:98` (`CreateDefaultInstance`). The default `Parse(ReadOnlySpan<char>)` is never emitted: descriptors inherit its body, so it must keep a body. |
| `IRecyclableDescriptor<T>.ParseInto(ReadOnlySpan<char>, int, T)` | `Emitter:60`, `:508` |
| `FileHelperRegistry<T>.SetDescriptor(IRecordDescriptor<T>)` | `Emitter:113-114` |
| `FieldDescriptor` constructor with 14 positional arguments: `(string, Type, int, int, int, string?, bool, string?, string?, bool, TrimMode, char, MultilineMode, string?)`; an array of `FieldDescriptor` wrapped in `ReadOnlySpan<FieldDescriptor>` | `Emitter:169`; `:90-91`, `:714` |
| `RecordOptions` parameterless creation (`new()`, a struct) and the `init` accessors `IgnoreFirst`, `IgnoreLast`, `IgnoreEmptyLines`, `CommentMarker`, `CommentAnyPlace`, `Delimiter`, `Condition`, `ConditionPattern`, `LineCountPerLogicalRecord`, `MultilineMode`, `IgnoreEmptySpaces`, `QuoteChar` | `Emitter:1409-1423` |
| Numeric values of `TrimMode`, `MultilineMode`, `RecordCondition` (emitted as casts from `int`) | `Emitter:169` (`TrimMode`, `MultilineMode`), `:1417` (`RecordCondition`), `:1420` (`MultilineMode`) |
| `RecordParsing.IndexOfLineBreak(ReadOnlySpan<char>, ReadOnlySpan<FieldDescriptor>, string)` | `Emitter:714` |
| `RecordFormatting.WriteFieldNewLine(IBufferWriter<char>)` | `Emitter:1321` |
| `BadUsageException(string)` and `BadUsageException(int, int, string)` | `(string)`: `Emitter:420`, `:739`, `:1045`, `:1256`; `(int, int, string)`: `:884`, `:901`, `:923-924` |
| `BadUsageException` as the type in a catch filter | `Emitter:649` |
| `ConvertException(string?, Type, string?, int, int, string?, Exception?)`; `ConvertException.MessageExtra` | `Emitter:1189`, `:1209` |
| `NullValueNotFoundException(int, int, string)` | `Emitter:1192` |
| `ISpanConverter<T>.TryParse` and `TryFormat`, also as a generic constraint | `TryParse`: `Emitter:604`, `:617`, `:645`; constraint and `TryFormat`: `:1280-1284` |
| Every built-in converter: the `Instance` field and the constructor shapes of `Emitter:291-345` (`BoolConverter(string, string)`, `CharConverter(string?)`, `GuidConverter(string?)`, the numeric converters with `(string)` and `(IFormatProvider?)`, the date and time converters with `(string?, CultureInfo?)`, `DateMultiFormatConverter(string[], CultureInfo?)`), `EnumConverter<T>.Instance` | class names `Emitter:246-284`; `.Instance` `:255`; `new(...)` `:257-258`; arguments `:291-345` |
| `ConverterBaseAdapter<T>(ConverterBase)`, `.CustomNullHandling`, `TryParse`, `TryFormat` | `Emitter:238-240`, `:629`; `TryParse` `:645`; `TryFormat` `:1284` |

The numeric converters are `ByteConverter`, `SByteConverter`, `Int16Converter`, `UInt16Converter`,
`Int32Converter`, `UInt32Converter`, `Int64Converter`, `UInt64Converter`, `DecimalConverter`,
`DoubleConverter`, `SingleConverter` and `PercentDoubleConverter`. The date and time converters are
`DateTimeConverter`, `DateOnlyConverter`, `TimeOnlyConverter`, `DateTimeOffsetConverter` and
`TimeSpanConverter`.

### Added in 4.10

| Member | Bound at |
|---|---|
| `FieldDescriptor.TrimGivenChars` `init` accessor, as an object initializer `new(...) { TrimGivenChars = true }` | `EmitFieldDescriptors` in `DescriptorEmitter.cs`; emitted only for a field with `[FieldTrim(..., TrimGivenChars = true)]` and a `TrimMode` other than `None`, so the code of every other record is unchanged |

A descriptor that sets it needs a 4.10 or later runtime, like any new API. The read helpers of the runtime
(`RecordParsing.IndexOfLineBreak` and the engine's quote scan) read the flag; with the default `false` they
behave as in 4.9.1.

A record whose attributes have errors that the build hides gets a descriptor from
`src/ModernLibs.FileHelpers.Generator/Emission/InvalidRecordEmitter.cs` instead. It binds to a subset of the
table: `IRecordDescriptor<T>` (`InvalidRecordEmitter:46`) and its five members (`:52-56`), `BadUsageException(string)`
(`:32`) and `FileHelperRegistry<T>.SetDescriptor` (`:64-65`).

## 4.10 members

The tokenized read contract (design 4.2 to 4.5, frozen by T14 on 2026-11-14). From 4.10 on the runtime finds
the field boundaries of a record once and hands them to generated code; generated code still emits its full
legacy `Parse` and `ParseInto`, which handle every record the tokenized path gives back. Both types are hidden
(`[EditorBrowsable(Never)]`), and their signatures and member semantics below are a contract like the table
above. The layout of `RecordTokens`, the field cap and the conditions under which the runtime tokenizes are
internal and may change in a patch.

```csharp
namespace ModernLibs.FileHelpers;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITokenizedDescriptor<T> : IRecordDescriptor<T>
{
    // false = "not handled" (never "invalid input"); a conversion or validation error throws.
    bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, [MaybeNullWhen(false)] out T result);
    bool TryParseInto(ReadOnlySpan<char> record, in RecordTokens tokens, T existing) => false;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref struct RecordTokens   // internal constructor; default has Count 0; no member throws
{
    public int Count { get; }
    public bool IsQuoted(int index);
    public ReadOnlySpan<char> GetValue(int index);
    public string GetString(int index);
}
```

| Member | Result |
|---|---|
| `TryParse`, `TryParseInto` returning `false` | "Not handled": the runtime validates the field count and calls the legacy `Parse` or `ParseInto` for the same record. A method returns `false` only before any converter runs (and, for `TryParseInto`, before any member is assigned), so a custom converter never runs twice for one record. A conversion or validation error throws, with the text, type and column of the legacy code. |
| `Count` | Fields found. The runtime calls `TryParse` only when the mandatory field count `<= Count <=` the field count. |
| `IsQuoted(i)` | `true` when field `i` is a `[FieldQuoted]` field and its value was quoted; `false` for `i` outside `[0, Count)`. |
| `GetValue(i)`, not quoted | `record.Slice(start, length)`, also when the length is 0: a present empty field keeps its position, so `RecordParsing.Column` gives the column of the legacy code. |
| `GetValue(i)`, quoted, no doubled quote | `record.Slice(start + 1, length - 2)`, also for `""`. |
| `GetValue(i)`, quoted, doubled quotes | The unescaped text in a scratch buffer, not part of the record (`Column` gives -1, as with the legacy pooled copy). |
| `GetValue(i)`, `i` outside `[0, Count)` | `default`: what the legacy code yields for a missing trailing field. |
| `GetString(i)` | The string of `GetValue(i)` by `RecordParsing.CreateString`, or by `RecordParsing.UnquoteToString` for doubled quotes; `string.Empty` for `i` outside `[0, Count)`. |

Lifetime: a `RecordTokens` value, and every span `GetValue` returns, is valid until the same reader tokenizes its
next record. All spans of one record stay valid together: the runtime sizes the scratch before it calls the
descriptor, and `GetValue` never reallocates it (a value that does not fit is a new array). Generated code never
stores the tokens or the spans; `RecordTokens` is a `ref struct`, so it cannot escape to the heap.

The runtime tokenizes a record type only when its descriptor implements `ITokenizedDescriptor<T>`, it is
delimited with a one-char delimiter and one line per record, no field has its own `[FieldDelimiter]`, no field is
quoted (4.10.0 adds quoted fields in T20), it has 1 to 4096 fields, and the switch below is off. A record of
2^30 chars or more, or with a field count out of range, takes the legacy path. 4.8 and 4.9 descriptors do not
implement the interface and always take the legacy path.

`AppContext` switches, read once into a static field before the first engine is created (a safety valve, may be
removed in 5.0): `ModernLibs.FileHelpers.DisableTokenizedParse` (the runtime never calls
`ITokenizedDescriptor<T>`) and `ModernLibs.FileHelpers.DisableVectorizedTokenizer` (the tokenizer uses no vector
code of its own, only `IndexOf`, which the BCL may vectorize).

### Bound by 4.10 generated code

From 4.10 (T17) the generator emits the interface and calls the runtime read helpers below instead of copying
`__Column`, `__ConvertError`, `__NullValueNotFound` and `__ConverterFailure` into every descriptor and unescaping
quoted values in a pooled buffer. A descriptor generated by 4.10 therefore needs a 4.10 or later runtime. "Bound
at" names the emitter method in `DescriptorEmitter.cs`.

| Member | Bound at |
|---|---|
| `ITokenizedDescriptor<T>` in the interface list, for a delimited record with one line per record and at least one field (with or without `[FieldQuoted]` fields); never for fixed-length or `[FieldInNewLine]` records | `Generate` |
| `ITokenizedDescriptor<T>.TryParse(ReadOnlySpan<char>, in RecordTokens, out T)`; `TryParseInto(ReadOnlySpan<char>, in RecordTokens, T)`, emitted exactly when `ParseInto` is | `EmitTryParseBody` |
| `RecordTokens.Count` and `IsQuoted(int)`: `if (tokens.Count > i && !tokens.IsQuoted(i))` gives the record back for an `AlwaysQuoted` or `OptionalForWrite` field, before any value is read | `EmitTryParseBody` (step 1) |
| `RecordTokens.GetValue(int)` for every field the later passes read; `RecordTokens.GetString(int)` for a `string` field without `[FieldTrim]` and `[FieldNotEmpty]` | `EmitTryParseBody` (step 2), `EmitFieldConversion` |
| `RecordParsing.CreateString(ReadOnlySpan<char>)` | `EmitFieldConversion`, `EmitFieldQuotedStripPass` |
| `RecordParsing.Unquote(ReadOnlySpan<char>, char)`; `RecordParsing.UnquoteToString(ReadOnlySpan<char>, char)` for a quoted `string` field without `[FieldTrim]` and `[FieldNotEmpty]` | `EmitFieldQuotedStripPass` (legacy `Parse` and `ParseInto`, delimited and fixed-length) |
| `RecordParsing.Column(ReadOnlySpan<char>, ReadOnlySpan<char>)` in the layout errors of quoted fields | `EmitQuoteAwareDelimitedSplitting` |
| `RecordParsing.ConvertError(ReadOnlySpan<char>, ReadOnlySpan<char>, Type, string, string?, Exception?)` | `EmitFieldConversion`, `EmitFieldNotEmptyValidation` |
| `RecordParsing.NullValueNotFound(ReadOnlySpan<char>, ReadOnlySpan<char>, string, string)`, the last argument the record type name | `GetEmptyFieldStatement` |
| `RecordParsing.ConverterFailure(ReadOnlySpan<char>, ReadOnlySpan<char>, Type, string, string, Exception)` | `EmitFieldConversion` (custom converters) |

The legacy `Parse` and `ParseInto` are still emitted in full; they serve every record the tokenized path gives
back. The read side of a 4.10 descriptor no longer uses `ArrayPool<char>`, `Unsafe` or `MemoryMarshal`; from T23
the write side uses none of them either (write contract below).

Tests: `RecordTokensTests` (member semantics), `RecordTokenizerTests` (the vector and the `IndexOf` paths against
`string.Split`, at vector block edges), `TokenizedReadTests` (every engine reads the same records and errors on
the tokenized and the legacy path, with a checking descriptor on the `Compat49Matrix` shapes),
`TokenizedEmissionTests` (the emitted text: which records get the interface, the order of the tokenized bodies,
no pool and no copied helpers in the legacy bodies), `TokenizedGeneratedReadTests` (the generated bodies at run
time against the legacy ones, including quoted tokens written by hand) and `GeneratedCodeContractTests` (the
members of this section).

### Write contract

The write contract (design 5.1 and 5.2, frozen by T22 on 2026-11-16, `t22-freeze-2026-11-16.md`). A descriptor
that implements `IRecordWriterDescriptor<T>` formats a record straight into the engine's write buffer through a
`RecordWriter`; generated code calls the converter itself, so no generic method stands between it and
`TryFormat`. All three types are hidden (`[EditorBrowsable(Never)]`); their signatures, the member semantics
below, the emitted call pattern of a converted field and the exception types and texts are a contract like the
tables above. `RecordWriteBuffer`, the fields of `RecordWriter` and every growth size above the stated minimum
are internal and may change in a patch.

```csharp
namespace ModernLibs.FileHelpers;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRecordWriterDescriptor<T> : IRecordDescriptor<T>
{
    void Format(T record, ref RecordWriter writer);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct RecordWriter   // the constructor with arguments is internal
{
    public int Position { get; }
    public Span<char> FreeSpan { get; }
    public void Advance(int count);
    public void Grow(object converter);
    public void Reserve(int count);
    public void Write(char value);
    public void Write(scoped ReadOnlySpan<char> text);
    public void WriteNewLine();
    public void Quote(int fieldStart, char quote, bool always, bool multilineWrite, string separator, string field);
    public void WriteQuoted(scoped ReadOnlySpan<char> text, char quote, bool always, bool multilineWrite, string separator, string field);
    public void Pad(int fieldStart, int width, int align, char pad, bool overflowError, string field);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class RecordFormatting   // existing: WriteFieldNewLine
{
    public static void Format<T>(IRecordWriterDescriptor<T> descriptor, T record, IBufferWriter<char> writer);
}
```

A converted field is written as
`while (!converter.TryFormat(value, writer.FreeSpan, out n)) writer.Grow(converter); writer.Advance(n);`.

| Member | Semantics |
|---|---|
| `default(RecordWriter)` | Every member, the two getters included, throws `InvalidOperationException`. |
| `Position` | The chars of this record written so far; 0 when `Format` is entered. Growth never changes it: the record stays contiguous and a `fieldStart` read earlier stays valid. |
| `FreeSpan` | The writable space at `Position`, of any length, also 0. A span read earlier must not be used after any other member was called. |
| `Advance(count)` | `Position` grows by `count`; `count` below 0 or above `FreeSpan.Length` throws `ArgumentOutOfRangeException`. |
| `Grow(converter)` | When `FreeSpan.Length` is 0x100000 or more it throws `FormatException("The field value could not be formatted by " + converter.GetType().Name + ".")`, the 4.9 text; otherwise the free space becomes at least twice as large and at least 64 chars. So a value of up to 0x100000 chars is always written, and a converter that never fits gets the 4.9 exception. Unlike 4.9, a value above 0x100000 chars is written when the free space happens to be large enough. |
| `Reserve(count)` | Capacity hint; no text and no `Position` change. |
| `Write(char)`, `Write(text)` | Append; grow as needed; `Write` does not keep `text`. |
| `WriteNewLine()` | Appends the engine's `NewLineForWrite`; through `RecordFormatting.Format` into another `IBufferWriter<char>`, `"\r\n"`. Nothing when it is empty. |
| `Quote(fieldStart, ...)` | Quotes the chars from `fieldStart` to `Position` in place; result and `BadUsageException` text equal `__WriteQuotedField` and `__WriteQuoted` of 4.9. |
| `WriteQuoted(text, ...)` | Appends `text` as a quoted field (freeze Revision 1, T23). The text written, `Position` afterwards and the errors equal `Write(text)` followed by `Quote` from the `Position` before the call: `__WriteQuotedField` of 4.9 for `text`. The text is tested in `text` and copied once. `Position` after the `BadUsageException` is not specified (the engine discards the record). Does not keep `text`, which must not be part of the writer's own storage. |
| `Pad(fieldStart, ...)` | Pads or cuts the chars from `fieldStart` to `Position` in place; result and `InvalidOperationException` text equal `__WritePadded` of 4.9; afterwards `Position` is `fieldStart + width`. |
| `RecordFormatting.Format<T>` | The body of the legacy `Format(T, IBufferWriter<char>)` of a 4.10 descriptor: appends what `Format(record, ref writer)` writes. Into an engine write buffer it takes the buffer's `NewLine`; `null` for `descriptor` or `writer` throws `ArgumentNullException`. When the descriptor throws, nothing is written. |

The runtime keeps the text of a record only when `Format(record, ref writer)` returns: a record whose formatting
throws leaves nothing, as in 4.9. The engines call it for every write (`WriteString`, `WriteStream`, `WriteFile`,
`AppendToFile`, `WriteStreamAsync`, `FileHelperAsyncEngine<T>.WriteNext`, the multi-record and master-detail
engines), with or without write events; without an `AfterWriteRecord` handler and `INotifyWrite<T>`,
`WriteStreamAsync` formats the record straight into its pending block. The `AppContext` switch
`ModernLibs.FileHelpers.DisableRecordWriter` makes them call the legacy `Format` instead. 4.8 and 4.9
descriptors do not implement the interface and keep the 4.9 path.

Members added to `IRecordWriterDescriptor<T>` after 4.10.0 come with a default implementation.

From T23 every generated descriptor of a valid record implements the interface (after its other interfaces) and
binds to the members above as follows. The descriptor generated for a record with FileHelpers errors (hidden with
`[SuppressMessage]` or `RunAnalyzers=false`) implements `IRecordDescriptor<T>` only; every member of it throws, so
the engines take the legacy path and throw the same `BadUsageException`. A 4.10 descriptor no longer emits the
4.9 helpers `__WriteText`, `__WriteQuoted`, `__WriteQuotedField`, `__WritePadded` and `__Format`, the `stackalloc`
buffer per field, `ArrayPool<char>`, or the call of `RecordFormatting.WriteFieldNewLine`.

| Member | Bound at |
|---|---|
| `IRecordWriterDescriptor<T>` in the interface list of every descriptor of a valid record | `Generate` |
| `RecordFormatting.Format<T>`: the whole body of the legacy `Format(T, IBufferWriter<char>)` is `global::ModernLibs.FileHelpers.RecordFormatting.Format(this, record, writer);` | `EmitLegacyFormat` |
| `Reserve(int)`: first statement of a fixed-length record, with the sum of the field widths; `Reserve(64)` before the converter loop of a custom `ISpanConverter<T>` field | `EmitFormatBody`, `EmitConverterLoop` |
| `Write(char)` for a one-char delimiter, `Write(ReadOnlySpan<char>)` for a longer one (a string literal) and for a `string` field (`MemoryExtensions.AsSpan(record.Name)`, empty for `null`) | `EmitDelimiterWrite`, `EmitFieldWrite` |
| `WriteNewLine()` before a `[FieldInNewLine]` field (after its delimiter) | `EmitFormatBody` |
| `Position`, read into `int __s{i}` before a quoted field with a converter, a fixed-length field or a `[FieldAlign]` field only | `EmitFieldWrite` |
| `WriteQuoted(ReadOnlySpan<char>, char, bool, bool, string, string)` for a quoted `string` field of a delimited record (`MemoryExtensions.AsSpan(record.Name)`, empty for `null`), with the arguments of 4.9's `__WriteQuotedField`; no `__s{i}` for it | `EmitFieldWrite` |
| `FreeSpan`, `Grow(object)`, `Advance(int)`: the converter loop above, with the converter expression (`.Instance` of a built-in converter or the `static readonly` field of the generated holder) as receiver and as argument of `Grow`; for a `Nullable<T>` field the loop and `Advance` are inside `if (record.X is { } __nv{i}) { ... }` | `EmitConverterLoop`, `EmitFieldWrite` |
| `Quote(int, char, bool, bool, string, string)` after a quoted field with a converter, with the arguments of 4.9's `__WriteQuotedField`; `Pad(int, int, int, char, bool, string)` after a fixed-length or `[FieldAlign]` field, with those of `__WritePadded`. Both run for a `null` value too. | `EmitFieldWrite` |

The converter is called directly (`converter.TryFormat(...)`). Two exceptions keep what custom converters had in
4.9 (T22 freeze, notes of Ruling P24):

- A custom `ISpanConverter<T>` whose `TryFormat` implements the interface only explicitly (or not publicly) is
  called through `__TryFormat<TConverter, TValue>`, a private static helper with the interface constraint that is
  emitted only into such a descriptor (no cast, so a struct converter is not boxed). Every converter that compiled
  with 4.9 compiles with 4.10. (`TryParse` is called on the converter type, as since 4.8.)
- A custom `ISpanConverter<T>` gets at least 64 free chars for its first `TryFormat`, as the 4.9 stack buffer gave:
  the emitted loop calls `Reserve(64)` first. Built-in converters and `ConverterBaseAdapter<T>` start from
  `FreeSpan` as it is.

Tests: `RecordWriterTests` (each member against the 4.9 helpers copied in `LegacyEmittedWriteCopies`),
`WritePathTests` (every engine writes the same text on the new and the legacy path, with a checking descriptor on
the `Compat49Matrix` shapes, events and custom newlines; the engines of the public constructors choose the record
writer of a generated descriptor; 4.8 and 4.9 descriptors still get a `RentedBufferWriter` with `NewLine` set),
`WriterEmissionTests` (the emitted text; an explicit `TryFormat`; a converter without a length test), `Compat49Tests` (the 4.9.1 descriptors against the generated ones and
Golden49; the record writer against the legacy writer on the matrix), `GeneratedCodeContractTests` and the fuzz
target `paths-switch` (the switch).

## Behaviour that old descriptors rely on

1. `WriteFieldNewLine` finds the engine's newline with `writer is RecordWriteBuffer` (from 4.10; `RentedBufferWriter`
   before) (`src/ModernLibs.FileHelpers/Descriptors/RecordFormatting.cs:24`). An old descriptor must always get a
   `RentedBufferWriter` (a `RecordWriteBuffer`) whose `NewLine` is set. A new write path must keep that path for
   descriptors that do not implement it.
2. `GetSpan(n)` returns one contiguous span of at least `n` chars
   (`src/ModernLibs.FileHelpers/Descriptors/IRecordDescriptor.cs:42-45`). Generated `Format` code writes into
   that span without checking its length.
3. A layout exception from generated code carries line -1 and a column from `__Column` (4.8, 4.9) or
   `RecordParsing.Column` (4.10). The engine attaches
   the line (`src/ModernLibs.FileHelpers/Errors/RecordErrors.cs:17-26`) and uses the column to find the
   physical line of a field in a multi-line record (`src/ModernLibs.FileHelpers/Engines/RecordReadCore.cs:389-397`).
4. The runtime validates the field count before it calls `Parse`
   (`src/ModernLibs.FileHelpers/Engines/RecordReadCore.cs:333-335`, `ValidateFieldCount`; for
   `[FieldInNewLine]` records, per physical line in `ParseGroup`). Old descriptors do not check it: a missing
   trailing field reads as empty in the generated splitter.
5. Do not add a constructor overload to a converter without checking that the emitted argument lists (for
   example `new(null, culture)` or `new("yyyy-MM-dd", null)`) still bind to the same overload when the
   consumer recompiles. A new overload can make `null` ambiguous (a compile error in the consumer) or bind it
   to a different parameter.

## Tests

- `GeneratedCodeContractTests.Every_Listed_Member_Exists` reflects over the library: each type of the table is
  public, each member exists with exactly the listed parameter types (compared one by one, not through the
  default binder, which would accept a widened type), the `RecordOptions` setters are `init`, and the enum
  values are as listed.
- `Compat48Tests` runs the 4.8.0 descriptors in `Generated48/` against the current runtime.
- `Compat49Tests` runs the 4.9.1 descriptors in `Generated49/` for 19 record shapes (all quote modes,
  `MultilineMode.NotAllow`, `[FieldInNewLine]`, `[FieldTrim]` with characters, `[FieldOptional]`,
  `[FieldDelimiter]`, a struct, a constructor, a recyclable class, the four `FixedMode` values with both
  `OverflowMode` values, `ConverterBase` and `ISpanConverter<T>` converters, and a 25-column record). For each
  input it builds a canonical text: the records, the errors, `HeaderText`, `FooterText`, `LineNumber` and
  `TotalRecords` of an array read (`Parse`), of a callback read with `RecycleRecords` (`ParseInto`) and of a
  read with `ErrorMode.ThrowException`, the records written back, and hand-made records that reach the
  write-only branches (`Compat49WriteCases`). It checks the text against the 4.9.1 golden files in `Golden49/`
  and against the text of the descriptor that the current generator makes. How the snapshots and the golden
  files were made: `Generated49/README.md`.

A change to any member above, or to the behaviour of the engine around them, must keep these tests green
without changing the snapshot or the golden files. The tests check the shapes and inputs they list, not every
record a consumer can declare.
