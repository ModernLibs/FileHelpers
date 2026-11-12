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

## Behaviour that old descriptors rely on

1. `WriteFieldNewLine` finds the engine's newline with `writer is RentedBufferWriter`
   (`src/ModernLibs.FileHelpers/Descriptors/RecordFormatting.cs:24`). An old descriptor must always get a
   `RentedBufferWriter` whose `NewLine` is set. A new write path must keep that path for descriptors that do
   not implement it.
2. `GetSpan(n)` returns one contiguous span of at least `n` chars
   (`src/ModernLibs.FileHelpers/Descriptors/IRecordDescriptor.cs:42-45`). Generated `Format` code writes into
   that span without checking its length.
3. A layout exception from generated code carries line -1 and a column from `__Column`. The engine attaches
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
