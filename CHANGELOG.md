# Changelog

All notable changes to ModernLibs.FileHelpers are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Versioning

ModernLibs.FileHelpers 4.x is a ground-up modern reimplementation that follows the FileHelpers 3.x API,
so its version numbers start at 4.

- **4.0.0 to 4.7.0 were development milestones.** They were published only as source releases on
  GitHub, never on NuGet. Their entries below are kept for history.
- **4.8.0 is the first release on NuGet.** It is a release-readiness milestone: it fixes behaviour to
  match FileHelpers 3.x, removes APIs that did not work, and freezes the public API.
- From 4.8.0 on, releases follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

If you are coming from FileHelpers 3.x, read the [migration guide](docs/MIGRATION.md) rather than this
file.

## [Unreleased]

## [4.8.0] - 2026-10-20

First NuGet release.

### Upgrading from a 4.x milestone

4.8.0 changes behaviour that earlier milestones got wrong. Code built against 4.7.0 or earlier may
need changes:

- Bad data is now an error. A field that cannot be converted raises `ConvertException` instead of
  silently becoming `default`, and an empty field for a non-nullable value type raises
  `NullValueNotFoundException` unless the field has `[FieldNullValue]`. Use `T?` for optional values.
- Errors use the FileHelpers 3.x exception types (`FileHelpersException`, `BadUsageException`,
  `ConvertException`, `NullValueNotFoundException`) instead of `FormatException`.
- `DateTime` fields without a format now use `ddMMyyyy`, the FileHelpers 3.x default, for reading and
  writing (was `MM/dd/yyyy HH:mm:ss`).
- `bool` fields no longer accept `yes`/`no` by default, and decimal, double, float and percent fields
  no longer accept currency symbols or `(5)`-style negatives.
- `ConverterKind.PercentDouble` works as in FileHelpers 3.5.2: a value that contains `%` raises
  `ConvertException`, and values are written without `%` (the milestones stripped a trailing `%` on
  read and appended one on write). Plain numbers read and write unchanged; nothing is divided by 100.
- **Breaking:** `[FieldQuoted]` fields allow line breaks inside quotes by default, as in 3.x (the
  milestones required `MultilineMode` on `[DelimitedRecord]`). Set
  `[DelimitedRecord(",", MultilineMode = MultilineMode.NotAllow)]` or use a `[FieldQuoted]` overload
  with a `MultilineMode` to keep values on one line.
- **Breaking:** `QuoteMode` is applied with 3.x errors: `AlwaysQuoted` and `OptionalForWrite` reject
  unquoted values; an unclosed quote, blanks before the quote (unless the field is trimmed at the
  start) and text between the closing quote and the delimiter raise `BadUsageException`.
  `OptionalForWrite` / `OptionalForBoth` quote only values that contain the delimiter or a line break.
  Quotes in fields without `[FieldQuoted]` are plain text.
- `[IgnoreCommentedLines]` skips indented comment lines by default (`anyPlace` defaults to `true`).
- `MultiRecordEngine` and `MasterDetailEngine` skip lines that match no selector instead of failing.
- `RecycleRecords` no longer affects array-returning reads.
- **Breaking:** `internal` and `protected internal` fields and properties are now part of the layout
  (they were skipped), closer to 3.x, which maps every instance field. Members you kept out of the
  layout by making them `internal` now need `[FieldHidden]`. `private`, `protected` and
  `private protected` members cannot be reached by generated code: annotated ones fail the build with
  `MLFH023`, and unannotated ones that 3.x would map as columns are reported as `MLFH028` (warning).
- **Breaking:** `[FieldDelimiter]` sets the delimiter that *follows* its field, as in 3.x: with
  `[DelimitedRecord(",")]`, `{ A; [FieldDelimiter("|")] B; C }` is `a,b|c`. The milestones used it as
  the delimiter before the field (`a|b,c`). Files written by a milestone with this layout are now
  rejected with a field-count error instead of being misread; to keep such a layout, move the
  attribute one field earlier. FileHelpers 3.x code works unchanged.
- **Breaking:** `[FieldOrder]` must be on every field or on none, as in 3.x (`MLFH024`; a partial
  order used to be completed silently).
- **Breaking:** `[FieldQuoted]` on a field of a fixed-length record is an error (`MLFH025`), as in
  3.x; it used to be accepted and ignored.
- **Breaking:** consuming projects need C# 11 or later (`MLFH026`). The .NET 10 default is C# 14, so
  this only affects projects that pin an older `LangVersion`.
- **Breaking:** `ref struct` record types are rejected (`MLFH027`). The engines could not use them.
- **Breaking:** a `[FieldConverter(typeof(X))]` whose type is not a converter for the field (`MLFH004`)
  is an error; it was a warning and the converter was ignored. As for `MLFH006` and `MLFH010`, the
  record gets no descriptor until the attribute is fixed.
- `FileHelperAsyncEngine<T>` throws `InvalidOperationException` when a read or write session overlaps
  another one on the same instance (`ReadAsync`, `BeginRead*`, `BeginWrite*`). Use one engine instance
  per concurrent read or write.
- `ErrorManager.SaveErrors` writes 1-based column numbers, like the exception messages and
  `ConvertException.ColumnNumber` (`ErrorInfo.ColumnIndex` stays 0-based).
- `MultiRecordEngine` and `MasterDetailEngine` end records with their new `NewLineForWrite` property
  (default `"\r\n"`, like the other engines) instead of the platform newline.
- Records with `[FieldInNewLine]` fields are written on several lines, in the 3.x layout, instead of on
  one line that could not be read back.
- **Breaking:** several public types and members were removed or hidden, including
  `DelimitedRecordAttribute.HasHeader`, `.TrimMode` and `.QuoteUnescapeMode` and `[FieldName]`; see
  [Removed](#removed).

### Added

- Exception types with FileHelpers 3.x names and messages: `FileHelpersException` (with `LineNumber`
  and `ColumnNumber`), `BadUsageException`, `NullValueNotFoundException` and `ConvertException` (with
  `FieldName`, `FieldType`, `FieldStringValue`, `LineNumber`, `ColumnNumber`, `MessageExtra`,
  `MessageOriginal`). Unlike 3.x, `BadUsageException` and `NullValueNotFoundException` have public
  constructors.
- Field types without a custom converter: nullable value types, enums (by name, case-insensitive),
  `TimeSpan`, `DateTimeOffset`, `Guid`, `uint`, `ulong`, `ushort`, `sbyte` and `char`, for reading and
  writing, delimited and fixed-length. New `TimeSpanConverter` and `DateTimeOffsetConverter`.
- Records without a usable parameterless constructor: positional records and classes are created
  through the constructor whose parameter names match the fields; init-only structs are created with
  an object initializer.
- Converter constructors matching 3.x: `BoolConverter(trueString, falseString)` (used by
  `[FieldConverter(ConverterKind.Boolean, "yes", "no")]`), `XConverter(string decimalSeparatorOrCultureName)`
  and `XConverter(IFormatProvider?)` for the numeric converters, a case argument for `CharConverter`
  (`x`/`lower`, `X`/`upper`) and a format argument for `GuidConverter` (`N`, `D`, `B`, `P`).
- `ConverterBase.FieldToString` is virtual and `ConverterBase.CustomNullHandling` is available, as in
  3.x. By default a custom converter is not called for empty input.
- `[FieldConverter(typeof(X), "a", ...)]` passes the arguments to a public constructor of `X` with that
  many `string` parameters (or `params string[]`), as in 3.x, without reflection.
- `[FieldQuoted]` constructors from 3.x: `(QuoteMode, MultilineMode)`, `(char, QuoteMode, MultilineMode)`
  and `(MultilineMode)`, and the `QuoteMultiline` property. `DelimitedRecordAttribute.MultilineMode`,
  when set, overrides the multiline mode of every quoted field in the record.
- `ErrorManager` and `ErrorMode` on `MasterDetailEngine` and `MasterDetailAsyncEngine`.
- `MultiRecordEngine.NewLineForWrite` and `MasterDetailEngine.NewLineForWrite` (default `"\r\n"`). The
  value is also used for the line breaks inside `[FieldInNewLine]` records.
- Record types with C# 11 `required` members. The generated code sets them, and adds nothing when the
  constructor it calls has `[SetsRequiredMembers]`.
- Diagnostics `MLFH010` to `MLFH029` (the full set is now `MLFH001` to `MLFH029`): incompatible
  `[FieldNullValue]`, conflicting record attributes, empty delimiter, unsupported field type, record
  that cannot be constructed, unassignable annotated member, generic record, inaccessible nested
  record, required field after an optional one, `ConverterKind` that does not match the field type,
  internal generator failure, `DateMultiFormat` without formats, a custom converter without a
  constructor matching its `[FieldConverter]` arguments, an annotated member that generated code
  cannot access (`private`, `protected`), `[FieldOrder]` on only some fields, `[FieldQuoted]` in a
  fixed-length record, a language version below C# 11, `ref struct` records, an unannotated
  `private` or `protected` member that 3.x would map as a column (warning), and a `[FieldInNewLine]`
  first field combined with `[IgnoreEmptyLines]` (warning). `MLFH006` now applies only when no
  arguments are given.
- A [diagnostics reference](docs/diagnostics.md) with cause, example and fix for every rule; each
  diagnostic's help link points to its section. All rules are shipped in the 4.8.0 analyzer release.
- The `MLFH007` width threshold can be set with the `FileHelpersMaxFieldWidth` MSBuild property or
  `filehelpers.max_field_width` in `.globalconfig` (the MSBuild property wins when both are set). The
  package ships `build/` and `buildTransitive/` props for it.
- XML documentation for every public member, and a public API baseline (`PublicAPI.*.txt`).

### Changed

- **One read pipeline.** All read paths (`ReadString`, `ReadSpan`, `ReadFile`, `ReadStream`,
  `ReadSync`, `ReadAsync`, callbacks, `ReadFileAsync`, the async engine, parallel, multi-record and
  master/detail reads) share one implementation and return the same records, `HeaderText`,
  `FooterText`, errors, line numbers and counters.
- **Empty fields** follow 3.x: non-nullable value types raise `NullValueNotFoundException`, `T?` fields
  become `null`, strings keep their text. `[FieldNullValue]` is applied to empty and blank values.
- **Conversion errors** raise `ConvertException`. Exceptions from custom converters are wrapped with the
  3.x message "Your custom converter: X throws an Y ...".
- **Layout errors**: too few delimited fields raise `FileHelpersException`; extra fields, a wrong
  fixed-length width, an incomplete `[FieldInNewLine]` record and an unclosed quote in any field
  raise `BadUsageException`; a `[FieldNotEmpty]` violation raises `ConvertException`.
- In `ErrorMode.ThrowException`, library exceptions are thrown as they are, with the line number; other
  exceptions are wrapped in `FileHelpersException` (was `FormatException`). In
  `ErrorMode.SaveAndContinue`, `ErrorInfo` has the field name, column and raw value of conversion
  errors.
- `ReadSync` no longer yields `default(T)` for struct records that fail, and does not count them.
  `FileHelperEngine<T>.ReadAsync` skips failed records instead of ending the enumeration.
- **Booleans** follow 3.x: trimmed, case-insensitive, `true`/`1`/`y`/`t` are true and `false`/`0`/`n`/`f`
  are false; an empty field follows the empty-field rule.
- **Dates**: the default format is `ConverterBase.DefaultDateTimeFormat` = `ddMMyyyy`, used for implicit
  `DateTime` fields and `ConverterKind.Date` without arguments, for reading and writing. Date
  converters trim their input. An empty or invalid format raises `BadUsageException` when the converter
  is created.
- **Numbers** use the 3.x number styles (`Number | AllowExponent`; `Number` for `byte`/`sbyte`), so
  `1,234`, `5.0`, `1e3` and `5-` are accepted and blanks around a leading sign are removed.
- **`char`** takes the first character without trimming and is `'\0'` when empty; longer values no
  longer fail. **`Guid`** accepts every `Guid` format and is `Guid.Empty` when empty.
- Built-in conversions never depend on the current culture.
- Converter arguments (date format and culture, decimal separator, bool strings, char and Guid formats,
  `CultureName`, custom `FieldToString`) are applied when writing as well as reading.
- **Field layout** follows 3.x: base-class fields come first; `public`, `internal` and
  `protected internal` fields and properties are mapped; `[IgnoreInheritedClass]` on any type of the
  inheritance chain stops the walk at that type; `[FieldHidden]` and `[FieldNotInFile]` members are
  left out of reading, writing and the field metadata; `[FieldValueDiscarded]` fields are not converted
  when read but are written.
- **`[FieldDelimiter]`** is the delimiter written and expected after its field, as in 3.x (see
  [Upgrading](#upgrading-from-a-4x-milestone)). Records with `[FieldDelimiter]` fields are now
  field-count validated like other records: a missing field raises `FileHelpersException` with the
  3.x description ("Delimiter '|' not found after field 'B'"), an extra delimiter after the last field
  `BadUsageException`. On the last field the attribute only affects that check and `OptionalForWrite`
  quoting.
- **`[FieldInNewLine]`** follows 3.x: such fields are written on a new line (after the delimiter in
  delimited records; the line break is the engine's `NewLineForWrite`) and read back, also together
  with `[FieldDelimiter]` (`a,b|` + line break + `c,d`) and with multiline `[FieldQuoted]` values. When
  reading, each physical line is checked against its own fields with each field's own quote rules,
  and empty, comment and `[ConditionalRecord]`-excluded lines between records are skipped on their own
  instead of being taken into the next record. Errors (layout, conversion, empty value-type fields and
  quote errors) are reported on the physical line that fails, with the column inside it; after a layout
  error the following lines are read as the start of the next record, as in 3.x, after the error is
  reported (with `ThrowException`, `ReadNext` continues with them on the next call). Multi-record and
  master/detail engines read such records the same way, with the following lines going through the
  selector again. A line before a new-line field must end with the delimiter that follows its
  last field, unless the new-line field is `[FieldOptional]` or the line ends with a quoted value
  (3.x). One difference from 3.x remains: a
  conversion error skips the whole record (3.x continues on the next line).
- **`INotifyRead<T>.BeforeRead`** runs on the instance that then receives the values, for class
  records with settable members (as in 3.x), so state set there is kept. Struct records, records built
  through a constructor and records with `init`-only members still get a separate default instance
  that is discarded.
- **`FileHelperAsyncEngine<T>` sessions are exclusive**: `ReadAsync`, `BeginRead*` and `BeginWrite*`
  throw `InvalidOperationException` while another read or write session is open on the same instance.
  A `ReadAsync` during a `BeginRead*` session used to reset that session's `LineNumber`,
  `TotalRecords`, `HeaderText` and errors.
- `ErrorManager.SaveErrors` writes the 1-based column (empty when unknown).
- `MultiRecordEngine` and `MasterDetailEngine` write `NewLineForWrite` (default `"\r\n"`) after each
  record instead of the platform newline (`"\n"` on Linux and macOS).
- `[FieldOptional]`: a line may contain any number of fields between the required ones and all of them;
  missing trailing fields are read as empty.
- **Quotes** follow 3.x: only `[FieldQuoted]` fields treat quotes as quotes, each with its own quote
  character, `QuoteMode` and multiline mode (default `AllowForBoth`). A quote opens a quoted value only
  at the start of the field; blanks before it are allowed only in fields trimmed at the start
  (`TrimMode.Both` or `Left`), elsewhere the quote is plain text. Quoted values that are trimmed are
  trimmed before unquoting. Writing a line break into a field whose multiline mode forbids writing
  raises `BadUsageException`. Multiline values keep the line terminator that separated them in the
  input.
- **Comment lines**: `[IgnoreCommentedLines(marker, anyPlace)]` works as in 3.x; with `anyPlace`
  (the default) blanks may precede the marker, otherwise the marker must be in the first column.
- `[ConditionalRecord]` regex conditions are case-insensitive, as in 3.x.
- **Footer**: `FooterText` holds the original last lines of the input, unparsed. Blank and comment
  lines inside quoted values are kept.
- **Multi-record and master/detail**: a line that matches no selector is skipped (3.x); `ErrorMode` is
  honoured with correct line numbers; multiline records work per record type; a detail line before
  any master raises `BadUsageException`.
- **Writing**: `BeginAppendFile` clears `HeaderText` and `FooterText`; `AppendToFile` adds a missing
  final newline to the file before appending; header and footer are written only when not empty,
  without a doubled newline.
- **Parallel reads** keep sequential semantics: `ErrorMode` behaviour, the earliest error is thrown,
  header and footer come from the whole input. Records that can span lines (including any
  `[FieldQuoted]` field that allows multiline reads, the default), `[FieldInNewLine]` and read events
  fall back to a sequential read. The progress callback runs once per chunk and never concurrently.
- **Package**: version 4.8.0 with accurate metadata; the assemblies carry the release version; only
  the library package is published (the generator is embedded in it).
- **Generated code** compiles with C# 11 and with `ImplicitUsings` disabled, and uses unique file
  names (no more lost output for records with the same name in different namespaces, or names that
  differ only in case). A failure in one record is reported as `MLFH020` instead of dropping the
  output for all records.
- **Diagnostics come from an analyzer** (`FileHelpersAnalyzer`, shipped in the generator assembly).
  They point at the offending code, so IDE squiggles work. `MLFH` warnings can be suppressed as usual.
  Errors that block the descriptor are not configurable: `#pragma warning disable`, `.editorconfig`
  severities and `NoWarn` do not hide them. If they are hidden with `[SuppressMessage]` or
  `RunAnalyzers=false`, the generator registers a descriptor that throws a `BadUsageException` naming
  the record and each error when the record is used. Only the errors `MLFH001` and `MLFH020` stay
  configurable. A `[FieldConverter]` or `[FieldNullValue]` that cannot be applied (`MLFH004`, `MLFH006`,
  `MLFH010`) blocks the descriptor too, so the record is never read without it; `MLFH004` is now an
  error (it was a warning, and the converter was ignored). A record that gets no descriptor at all
  (for example a private nested type or C# 10 with analyzers turned off) throws an
  `InvalidOperationException` whose message lists the likely causes and links the diagnostics
  reference. See
  [Suppressing diagnostics](docs/diagnostics.md#suppressing-diagnostics). `MLFH001` no longer fires on
  the original `FileHelpers.FileHelperEngine<T>`.
- A field whose type or converter type does not resolve reports only the compiler error (`CS0246`),
  not `MLFH013`, `MLFH004` or `MLFH019` as well.
- Performance: numeric fields are parsed on a fast path for plain invariant numbers, and before the
  blank check. Records made only of integer fields do not benefit and are currently slower than with
  the earlier parsing code; this is being investigated.
- Performance: a very long line read through `ReadAsync` takes linear time (a 16 MB line went from
  1.6 s to under 0.1 s), and streaming reads with `[IgnoreLast]` no longer allocate a string per line.

### Fixed

- Positional records and classes bound by constructor no longer get their values swapped.
- `[FieldNullValue]` was ignored.
- A custom converter that returns `null` for a field that accepts `null` no longer fails.
- Records that end before trailing `[FieldOptional]` fields are accepted.
- Long values are no longer truncated or written empty when they exceed an internal write buffer.
- `RecycleRecords` made array-returning reads return the same instance repeatedly.
- Read state is reset for every read: `ErrorManager` no longer accumulates errors across reads,
  `HeaderText` is captured by streaming reads, and `TotalRecords` no longer accumulates in the async
  engine.
- An unclosed quote at the end of the input is an error on every read path; errors report the first
  line of a multiline record.
- `ReadFileAsync` throws `OperationCanceledException` when cancelled.
- Async engine: reading struct records no longer loops forever at the end of the input;
  `ReadAsync` raises `AfterReadRecord`; `BeginReadFile` honours a byte order mark.
- Async file reading decodes before splitting lines, so a CRLF across a buffer boundary and UTF-16 or
  UTF-32 input are handled; the byte order mark is removed; incomplete trailing bytes are decoded as
  U+FFFD (or raise an error with a strict encoding).
- The generated splitter follows the engine's quote rule: `5" ruler` is plain text, not an open quote.
- A quoted value containing the delimiter (`"a,b",c`) is no longer rejected as "too many fields" in
  records whose fields are not `[FieldQuoted]`; quotes there are plain text, as in 3.x.
- `IgnoreCommentedLines.AnyPlace` was ignored (only column 0 was checked) and documented wrongly.
- Arguments of `[FieldConverter(typeof(X), args)]` were silently dropped and a parameterless
  constructor was required.
- `[FieldTrim]` trim characters were ignored. They now follow 3.x: they are skipped before the opening
  quote (`Left`/`Both`) and after the closing quote (`Right`/`Both`) of a quoted delimited field, and
  an unquoted value in an `OptionalForRead` field starts after them. The field value itself is trimmed
  of white space per `TrimMode`, as in 3.x; the characters do not apply to it, to fixed-length fields
  or to writing. `[FieldTrim(mode)]` without characters uses the 3.x white-space set.
- Generator: a `null` attribute argument (`[FieldConverter(kind, null)]`, `[FieldTrim(mode, null)]`)
  or two record types whose names differ only in case no longer make the compiler drop the output for
  every record (`CS8785`).
- IDE: editing a file no longer makes the generator fail with "SyntaxTree is not part of the
  compilation" on the next incremental run.
- An `internal` field was skipped silently, which shifted every following column of a fixed-length
  record.
- A record with `required` members did not compile (`CS9035`).
- A `T?` field with an `ISpanConverter<T>` converter could write `null` (as an empty value) but not
  read it back; an empty value now reads as `null`.
- A record built through a constructor that validates its arguments threw on every read once it
  implemented `INotifyRead<T>`: the default instance was created by passing `default` to every
  constructor parameter. It now uses a parameterless constructor or no constructor at all.
- `[IgnoreInheritedClass]` on an intermediate base class was ignored (only the record type was
  checked).
- An empty, comment or `[ConditionalRecord]`-excluded line before a `[FieldInNewLine]` record was
  grouped with it and the whole record was lost.
- `FileHelperAsyncEngine<T>.EndWriteString` dropped `FooterText`, and `BeginAppendFile` appended
  directly after a last line without a line terminator.
- Appending to a file that contains only a byte order mark (for example from `WriteFile` with no
  records) added an empty first line that then failed to read.
- `MultiRecordAsyncEngine.ReadAsync(Stream)` and `MasterDetailAsyncEngine.ReadAsync(Stream)` closed the
  caller's stream.
- An unclosed quote in a quoted field that is not the last one was reported as "Expected N fields but
  found M" (`FileHelpersException`) instead of `BadUsageException`.
- Records whose first field is `[FieldInNewLine]` can now be read. As in FileHelpers 3.x, such a
  record starts with a blank (whitespace-only) line and the first field is read from the next line;
  text on that first line raises `BadUsageException` ("Text … found before the new line of the
  field: …"). Writing already produced this layout, so written files now read back.
- Generated code raised `CS0162` (unreachable code) for a required `AlwaysQuoted` or
  `OptionalForWrite` field at the start of a line, which broke builds with `TreatWarningsAsErrors`.
  Generated code is now tested to compile without warnings at every warning level.
- An annotated write-only property, or one with a `private` or `protected` getter, was left out of the
  layout silently; it now reports `MLFH023`.
- `[FieldDelimiter]` was used as the delimiter before its field, so 3.x files were misread
  (`a,b|c` gave `A = "a,b"`, `B = "c"`) and 4.x output could not be read by 3.x.
- A multiline `[FieldQuoted]` value in a `[FieldInNewLine]` record was written but could not be read
  back (unclosed quote).
- Errors in `[FieldInNewLine]` records were reported on the first line of the record.
- A record that failed while `[IgnoreLast]` lines were held back was processed again on every following
  `ReadNext`, and the rest of the input was lost. `Close()` in an `AfterReadRecord` handler of such a
  read raised `NullReferenceException`.
- A `ReadAsync` enumeration abandoned without being disposed left `FileHelperAsyncEngine<T>` unusable
  after `Close()` or `Dispose()`.
- Benchmarks: the benchmark records now parse their data correctly (the header line was read as a
  record, dates did not match, quoted fields were not unquoted, and the "FileHelpers 3.x" records
  resolved to this library, so the 3.x comparison measured 4.x against itself). Datasets are fully
  deterministic and every `[GlobalSetup]` checks the parsed or written data, so a benchmark can no
  longer measure garbage. 4.8.0 results are published in
  [docs/benchmarks/v4.8.0.md](docs/benchmarks/v4.8.0.md).

### Removed

- Public types that were unused or did not work: `SimdDelimiterFinder` and `SimdLevel`,
  `DelimitedFieldEnumerator`, `PooledLineBoundaries`, `MultilineFieldBuffer`, `MultiLineBuffer`,
  `RecordPool<T>`, `ParseContext`, `FormatContext` and `[SpanRecord]`.
- **Breaking:** `DelimitedRecordAttribute.HasHeader`, `DelimitedRecordAttribute.TrimMode`,
  `DelimitedRecordAttribute.QuoteUnescapeMode`, the `QuoteUnescapeMode` enum, `FieldNameAttribute`
  (`[FieldName]`) and the matching `RecordOptions.HasHeader`, `.TrimMode` and `.QuoteUnescapeMode`.
  They were read by the generator but never applied, and FileHelpers 3.x has none of them. Per-field
  `[FieldTrim]` is not affected.
- `SpanLineReader`, `PipelineLineReader` and `FixedLengthSlicer` are internal; the parsing namespace
  has no public types.
- Infrastructure used by generated code is hidden from IntelliSense (`[EditorBrowsable(Never)]`) and
  not intended for direct use: `FileHelperRegistry<T>.SetDescriptor` (the first registration wins;
  `null` is rejected), `IRecyclableDescriptor<T>`, `RecordOptions`, `ConverterBaseAdapter<T>`,
  `RecordFormatting`, `RecordParsing` and the `FieldDescriptor` constructor.

### Known limitations

These limitations and differences from FileHelpers 3.x remain in 4.8.0; the
[migration guide](docs/MIGRATION.md) has the details and workarounds.

- `MultiRecordEngine` does not apply `[IgnoreFirst]` / `[IgnoreLast]`.
- In `[FieldInNewLine]` records, a conversion error skips the whole record instead of continuing on
  the next line.
- Record types declared by another source generator get no descriptor (the FileHelpers generator
  cannot see them).
- `FooterText` stays `null` when a streaming read stops before the end of the input.
- `DateOnly` and `TimeOnly` default to `MM/dd/yyyy` and `HH:mm:ss`, unlike `DateTime` (`ddMMyyyy`).
- `ConvertException.ColumnNumber` is not set for every kind of error.
- `[FieldConverter(typeof(X), args)]` arguments must be strings (3.x accepted any object).

## [4.7.0] - 2026-05-13

Milestone, not published to NuGet.

### Added

- Fuzz testing with SharpFuzz: four fuzz targets, a seed corpus and a CI workflow.
- Mutation tests for the core parsers.
- Error messages with field and column context.
- Diagnostics `MLFH006` to `MLFH009` with guidance on how to fix them.
- `INotifyRead<T>.BeforeRead` with `SkipThisRecord` and `StopReading`.
- A parity verification test suite.

### Changed

- Documentation updates (README, migration guide).

## [4.6.0] - 2026-05-12

Milestone, not published to NuGet.

### Added

- `IFileHelperEngine<T>` and `IFileHelperAsyncEngine<T>` interfaces.
- `[FieldCaption]` and `[IgnoreInheritedClass]` attributes.
- Per-field culture (`FieldConverterAttribute.CultureName`).
- `CommonSelector` presets for master/detail classification.
- `ReadRemaining()` and `BeginAppendFile()` on `FileHelperAsyncEngine<T>`.
- Gap analysis document.

## [4.5.0] - 2026-05-11

Milestone, not published to NuGet.

### Added

- `ReadAsync` (`IAsyncEnumerable<T>`) on `FileHelperEngine<T>`.
- Callback-based reads, including by-reference `SpanRecordCallback<T>` callbacks.
- `RecordPool<T>` for pooling class records (removed in 4.8.0).
- `[SpanRecord]` and `ref struct` records (removed in 4.8.0).
- Opt-in features documentation.

## [4.4.0] - 2026-05-11

Milestone, not published to NuGet.

### Added

- Parallel batch reading with progress reporting and cancellation.
- `ArrayPool` buffer reuse.
- SIMD-accelerated `DelimitedFieldEnumerator` (AVX2, NEON, `Vector<T>`) (removed in 4.8.0).
- Quote unescape modes and record-level trim options (removed in 4.8.0).
- Header row support with `[FieldName]` column mapping (removed in 4.8.0; it was not functional).
- BenchmarkDotNet comparison with Sep and Sylvan.

## [4.3.0] - 2026-05-10

Milestone, not published to NuGet.

### Added

- `MultiRecordAsyncEngine` and `MasterDetailAsyncEngine<TMaster, TDetail>`.
- Mutation testing with Stryker.NET.
- NuGet release pipeline with Source Link and symbol packages.
- BenchmarkDotNet regression suite with comparisons against Sep and Sylvan.

## [4.2.0] - 2026-04-29

Milestone, not published to NuGet.

### Added

- `FixedMode` enforcement and `OverflowMode` for fixed-length records.
- `MultiRecordEngine` for files with several record types.
- `MasterDetailEngine<TMaster, TDetail>` for grouped records.
- `CommonSelectorMode` enum and `CommonSelector` presets.

## [4.1.0] - 2026-04-21

Milestone, not published to NuGet.

### Added

- `[FieldAlign]`, the validation attributes (`[FieldNotEmpty]`, `[FieldValueDiscarded]`) and multi-line
  records (`[FieldInNewLine]`).
- `[ConditionalRecord]` with eight condition types.
- `INotifyRead<T>` and `INotifyWrite<T>`.
- Per-field delimiters (`[FieldDelimiter]`) and multiline quoted fields.
- A quick-start parity test suite.

## [4.0.0] - 2026-04-21

Milestone, not published to NuGet. The first milestone, including the project foundation.

### Added

- .NET 10 library with an incremental source generator (`ForAttributeWithMetadataName`, equatable
  pipeline) that registers generated descriptors through a module initializer in
  `FileHelperRegistry<T>`.
- `[DelimitedRecord]`, `[FixedLengthRecord]` and the field attributes; `IRecordDescriptor<T>`.
- Diagnostics `MLFH001` to `MLFH003`.
- Span-based parsing pipeline (line reader, field enumerator, fixed-length slicer).
- `ISpanConverter<T>` with built-in converters, and the write pipeline.
- `FileHelperEngine<T>`: `ReadFile`, `ReadStream`, `ReadString`, `WriteFile`, `WriteStream`,
  `WriteString` and `AppendToFile`.
- `FileHelperAsyncEngine<T>`: streaming over `System.IO.Pipelines`, `ReadAsync` and `RecycleRecords`.
- `ErrorMode` (`ThrowException`, `IgnoreAndContinue`, `SaveAndContinue`) and the `BeforeReadRecord`,
  `AfterReadRecord`, `BeforeWriteRecord` and `AfterWriteRecord` events.
- TUnit test suite on Microsoft.Testing.Platform.

[Unreleased]: https://github.com/ModernLibs/FileHelpers/compare/v4.8.0...HEAD
[4.8.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.8.0
[4.7.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.7.0
[4.6.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.6.0
[4.5.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.5.0
[4.4.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.4.0
[4.3.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.3.0
[4.2.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.2.0
[4.1.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.1.0
[4.0.0]: https://github.com/ModernLibs/FileHelpers/releases/tag/v4.0.0
