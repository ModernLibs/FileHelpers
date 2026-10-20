# Source generator diagnostics

The ModernLibs.FileHelpers source generator checks record types at compile time. Many layout mistakes
that FileHelpers 3.x reports at run time (with a `BadUsageException`) are build errors here.

All rules use the category `FileHelpers.Generator`. Each compiler message links to its section on
this page.

| Rule | Severity | Title |
|------|----------|-------|
| [MLFH001](#mlfh001) | Error | Missing record attribute |
| [MLFH002](#mlfh002) | Error | Missing or non-positive `[FieldFixedLength]` |
| [MLFH003](#mlfh003) | Error | Ambiguous field order |
| [MLFH004](#mlfh004) | Error | Unrecognised `[FieldConverter(typeof(X))]` type |
| [MLFH005](#mlfh005) | Error | Invalid `ConditionalRecord` regex pattern |
| [MLFH006](#mlfh006) | Error | `FieldConverter` target type lacks parameterless constructor |
| [MLFH007](#mlfh007) | Warning | `FieldFixedLength` exceeds configured maximum width |
| [MLFH008](#mlfh008) | Warning | `FieldOrder` has gaps in sequence |
| [MLFH009](#mlfh009) | Warning | Record type has no public fields or properties |
| [MLFH010](#mlfh010) | Error | `FieldNullValue` type is incompatible with the field type |
| [MLFH011](#mlfh011) | Error | Conflicting record attributes |
| [MLFH012](#mlfh012) | Error | Empty delimiter |
| [MLFH013](#mlfh013) | Error | Unsupported field type |
| [MLFH014](#mlfh014) | Error | Record type cannot be constructed |
| [MLFH015](#mlfh015) | Error | Annotated member cannot be assigned |
| [MLFH016](#mlfh016) | Error | Generic record types are not supported |
| [MLFH017](#mlfh017) | Error | Record type is not accessible |
| [MLFH018](#mlfh018) | Error | Required field after an optional field |
| [MLFH019](#mlfh019) | Error | `ConverterKind` does not match the field type |
| [MLFH020](#mlfh020) | Error | Descriptor generation failed |
| [MLFH021](#mlfh021) | Error | Invalid converter arguments |
| [MLFH022](#mlfh022) | Error | No converter constructor for the converter arguments |
| [MLFH023](#mlfh023) | Error | Annotated member is not accessible |
| [MLFH024](#mlfh024) | Error | `[FieldOrder]` must be used on all fields or none |
| [MLFH025](#mlfh025) | Error | `[FieldQuoted]` is only valid in delimited records |
| [MLFH026](#mlfh026) | Error | C# 11 or later is required |
| [MLFH027](#mlfh027) | Error | ref struct records are not supported |
| [MLFH028](#mlfh028) | Warning | Member mapped by FileHelpers 3.x is not part of the layout |
| [MLFH029](#mlfh029) | Warning | `[FieldInNewLine]` first field with `[IgnoreEmptyLines]` |

When a record has an error, the generator does not emit its descriptor. Other record types in the
same project are not affected.

The rules are reported by an analyzer (`FileHelpersAnalyzer`) that ships in the same assembly as the
source generator, so they appear as you type and point at the offending declaration. Only MLFH020 is
reported by the generator itself.

### Suppressing diagnostics

Warnings (MLFH007, MLFH008, MLFH009, MLFH028, MLFH029) and MLFH001 can be suppressed as usual:
`#pragma warning disable MLFHnnn`, `[SuppressMessage]`, an `.editorconfig` severity or `<NoWarn>`. MLFH020
can be suppressed the same way; the record then has no descriptor, and using it throws
`InvalidOperationException` (`No IRecordDescriptor<T> registered`).

Every other error means that the record has no working descriptor; this includes a `[FieldConverter]` or
`[FieldNullValue]` that cannot be applied (MLFH004, MLFH006, MLFH010, MLFH019, MLFH022), because the record
would otherwise be read without it. These errors are *not configurable*: `#pragma warning disable`,
`.editorconfig` severities, `<NoWarn>` and diagnostic suppressors do not hide them, and the build
fails until the record is fixed. Two paths can still hide them, and neither fails silently:

- `[SuppressMessage]` attributes, which the compiler applies to not-configurable diagnostics as well;
- turning analyzers off (`<RunAnalyzers>false</RunAnalyzers>`), which leaves only the generator.

In both cases the generator registers a descriptor for the record whose members all throw a
`BadUsageException` naming the record and each error, for example "No descriptor was generated for
'MyApp.Row' because the record has FileHelpers errors, which were hidden from the build ... MLFH024: Field
'B' in type 'MyApp.Row' must be marked with [FieldOrder] ...". Creating or using an engine for the record
throws it. This throwing descriptor cannot be generated for records that generated code cannot reference
(MLFH016 generic, MLFH017 inaccessible, MLFH027 `ref struct`, `static` classes, `file`-local types) or
when C# 11 is not available (MLFH026); for them the engine throws an `InvalidOperationException`
(`No IRecordDescriptor<T> registered`) whose message lists these causes and links this page.

### Records declared by other source generators

The analyzer validates record types in generated code like any other. The FileHelpers generator,
however, cannot see types that another source generator adds to the compilation (Roslyn runs generators
side by side on the user's code only), so such a record gets no descriptor and no diagnostic; using it
throws `InvalidOperationException` (`No IRecordDescriptor<T> registered`). For the same reason,
members that another generator adds to a `partial` record are not part of its layout. Declare record
types, and all their mapped members, in your own source files.

The snippets below assume `using ModernLibs.FileHelpers;` (and `using ModernLibs.FileHelpers.Converters;`
for converter types).

## MLFH001

**Missing record attribute** (Error)

**Cause:** a type is used as the type argument of `FileHelperEngine<T>` or `FileHelperRegistry<T>`, but
the type has neither `[DelimitedRecord]` nor `[FixedLengthRecord]`. The generator only creates a
descriptor for annotated types, so the engine has nothing to work with.

```csharp
public class Customer            // no record attribute
{
    public int Id;
    public string Name = "";
}

var engine = new FileHelperEngine<Customer>();   // MLFH001
```

**Fix:** annotate the type with the attribute that matches the file layout.

```csharp
[DelimitedRecord(",")]
public class Customer
{
    public int Id;
    public string Name = "";
}
```

For fixed-width files use `[FixedLengthRecord]` (or `[FixedLengthRecord(FixedMode.ExactLength)]`) and give
every field a `[FieldFixedLength(n)]`.

## MLFH002

**Missing or non-positive `[FieldFixedLength]`** (Error)

**Cause:** a field of a `[FixedLengthRecord]` type has no `[FieldFixedLength]`, or its width is zero or
negative. Without a width the column offsets of the record are undefined.

```csharp
[FixedLengthRecord]
public class Item
{
    [FieldFixedLength(5)] public int Code;
    public string Name = "";              // MLFH002: no width
}
```

**Fix:** add `[FieldFixedLength(n)]` with the number of characters the field occupies (`n > 0`).

```csharp
[FieldFixedLength(20)] public string Name = "";
```

Members marked `[FieldHidden]` or `[FieldNotInFile]` are not part of the layout and need no width.
Fields marked `[FieldValueDiscarded]` are read and then discarded, so they still need their width.

## MLFH003

**Ambiguous field order** (Error)

**Cause:** two fields of the same record have the same `[FieldOrder]` value, so the column order is
ambiguous.

```csharp
[DelimitedRecord(",")]
public class Order
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(1)] public string Product = "";   // MLFH003
}
```

**Fix:** give every field a unique `[FieldOrder]` value.

```csharp
[FieldOrder(1)] public int Id;
[FieldOrder(2)] public string Product = "";
```

## MLFH004

**Unrecognised `[FieldConverter(typeof(X))]` type** (Error)

**Cause:** `[FieldConverter(typeof(X))]` names a type that neither derives from `ConverterBase` nor
implements `ISpanConverter<T>` for the field type. The generator cannot use it, so the record gets no
descriptor (3.x throws a `BadUsageException` when the engine is created).

```csharp
public class NotAConverter { }

[DelimitedRecord(",")]
public class Row
{
    [FieldConverter(typeof(NotAConverter))]      // MLFH004
    public decimal Amount;
}
```

**Fix:** derive the converter from `ConverterBase` (the FileHelpers 3.x model), implement
`ISpanConverter<T>` for the field type (allocation-free), or use a built-in `ConverterKind`.

```csharp
public sealed class CentsConverter : ConverterBase
{
    public override object? StringToField(string text) =>
        decimal.Parse(text, CultureInfo.InvariantCulture) / 100m;
}

[FieldConverter(typeof(CentsConverter))] public decimal Amount;
```

## MLFH005

**Invalid `ConditionalRecord` regex pattern** (Error)

**Cause:** `[ConditionalRecord(RecordCondition.IncludeIfRegex, ...)]` or
`[ConditionalRecord(RecordCondition.ExcludeIfRegex, ...)]` has a pattern that is not a valid .NET
regular expression.

```csharp
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfRegex, "([A-Z]")]   // MLFH005: unbalanced parenthesis
public class Line { public string Text = ""; }
```

**Fix:** correct the pattern, or use a non-regex condition (`IncludeIfBegins`, `IncludeIfContains`,
`IncludeIfEnds`, `ExcludeIfBegins`, `ExcludeIfContains`, `ExcludeIfEnds`).

```csharp
[ConditionalRecord(RecordCondition.IncludeIfRegex, "^[A-Z]")]
```

## MLFH006

**`FieldConverter` target type lacks parameterless constructor** (Error)

**Cause:** the converter type in `[FieldConverter(typeof(X))]` (without converter arguments) is a valid
converter (`ConverterBase` or `ISpanConverter<T>`), but it has no public parameterless constructor. The
generated code creates the converter with `new X()`.

```csharp
public sealed class PrefixConverter : ConverterBase
{
    private readonly string _prefix;
    public PrefixConverter(string prefix) => _prefix = prefix;   // only constructor
    public override object? StringToField(string text) => _prefix + text;
}

[DelimitedRecord(",")]
public class Row
{
    [FieldConverter(typeof(PrefixConverter))] public string Code = "";   // MLFH006
}
```

**Fix:** add a public parameterless constructor to the converter, pass the constructor arguments in the
attribute (`[FieldConverter(typeof(PrefixConverter), "ID-")]`, see [MLFH022](#mlfh022)), or use a built-in
`ConverterKind`.

## MLFH007

**`FieldFixedLength` exceeds configured maximum width** (Warning)

**Cause:** a `[FieldFixedLength(n)]` width is larger than the configured maximum (default: 1000
characters). Very wide fields are often a configuration error, for example a byte count used as a
character count.

```csharp
[FixedLengthRecord]
public class Blob
{
    [FieldFixedLength(5000)] public string Payload = "";   // MLFH007 with the default maximum
}
```

**Fix:** reduce the width, or raise the threshold for the project when the width is intended:

```xml
<PropertyGroup>
  <FileHelpersMaxFieldWidth>8000</FileHelpersMaxFieldWidth>
</PropertyGroup>
```

or in a `.globalconfig` file:

```ini
is_global = true
filehelpers.max_field_width = 8000
```

If both are set, the `FileHelpersMaxFieldWidth` MSBuild property takes precedence over
`filehelpers.max_field_width` in `.globalconfig`; a value that is not a positive integer is ignored and the
next source (then the default of 1000) applies.

## MLFH008

**`FieldOrder` has gaps in sequence** (Warning)

**Cause:** explicit `[FieldOrder]` values skip numbers (for example 1, 2, 5). This often means a field
was deleted or an attribute was mistyped. Orders used by `[FieldHidden]` members do not count as gaps.
The record works: fields are read in ascending order.

```csharp
[DelimitedRecord(",")]
public class Row
{
    [FieldOrder(1)] public int A;
    [FieldOrder(2)] public int B;
    [FieldOrder(5)] public int C;     // MLFH008: gap between 2 and 5
}
```

**Fix:** renumber the fields consecutively, or suppress the warning if the gap is intentional
(`#pragma warning disable MLFH008` or `<NoWarn>$(NoWarn);MLFH008</NoWarn>`).

## MLFH009

**Record type has no public fields or properties** (Warning)

**Cause:** a type has a record attribute but no members that can be mapped. Its descriptor can neither
read nor write any data.

```csharp
[DelimitedRecord(",")]
public class Empty { }    // MLFH009
```

**Fix:** add at least one public field or property, or remove the record attribute if the type is not
meant to describe a file.

## MLFH010

**`FieldNullValue` type is incompatible with the field type** (Error)

**Cause:** the constant in `[FieldNullValue(value)]` cannot be converted to the field type. The value is
assigned when the field is empty, so it must fit the field; the record gets no descriptor until it does.

```csharp
[DelimitedRecord(",")]
public class Row
{
    [FieldNullValue("n/a")] public int Count;    // MLFH010: string constant for an int field
}
```

**Fix:** use a constant of the field type, or remove the attribute. A `double` constant is accepted for
a `decimal` field, because attributes cannot hold `decimal` constants.

```csharp
[FieldNullValue(-1)] public int Count;
[FieldNullValue(0.0)] public decimal Price;
```

## MLFH011

**Conflicting record attributes** (Error)

**Cause:** a type has both `[DelimitedRecord]` and `[FixedLengthRecord]`. A record describes exactly one
layout.

```csharp
[DelimitedRecord(",")]
[FixedLengthRecord]                // MLFH011
public class Row { public string A = ""; }
```

**Fix:** keep only the attribute that matches the file.

## MLFH012

**Empty delimiter** (Error)

**Cause:** `[DelimitedRecord("")]` or `[FieldDelimiter("")]` has an empty delimiter. At least one character
is needed to split fields.

```csharp
[DelimitedRecord("")]              // MLFH012
public class Row { public string A = ""; }
```

**Fix:** use a non-empty delimiter such as `","`, `";"`, `"|"` or `"\t"`.

## MLFH013

**Unsupported field type** (Error)

**Cause:** a mapped field has a type with no built-in conversion and no `[FieldConverter]`. Built-in
conversions exist for `string`, `bool`, `char`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`, `float`, `double`, `decimal`,
`DateTime`, `DateOnly`, `TimeOnly`, `DateTimeOffset`, `TimeSpan`, `Guid`, enums, and the nullable forms of
these value types. `ReadOnlySpan<char>` members are not supported (records cannot be `ref struct` types, see [MLFH027](#mlfh027)).

```csharp
[DelimitedRecord(",")]
public class Row
{
    public Uri Link = null!;       // MLFH013
}
```

**Fix:** add `[FieldConverter(typeof(MyConverter))]` with a `ConverterBase` or `ISpanConverter<T>`
implementation, change the field type, or exclude the member with `[FieldHidden]`.

A field or converter type that does not resolve (`CS0246`) is not reported as MLFH013 or MLFH004:
the compiler error is enough, and no descriptor is generated until it is fixed.

## MLFH014

**Record type cannot be constructed** (Error)

**Cause:** the generated descriptor cannot create instances of the record. It needs one of:

- an accessible (public or internal) parameterless constructor, used with an object initializer; or
- an accessible constructor whose parameters all bind to mapped members by name (case-insensitive), to
  `[FieldHidden]` members, or have default values.

Abstract types cannot be records.

```csharp
[DelimitedRecord(",")]
public class Row
{
    public Row(int id, string unrelated) { Id = id; }   // 'unrelated' matches no member
    public int Id { get; set; }
}                                                         // MLFH014
```

**Fix:** add an accessible parameterless constructor, or make every constructor parameter match a mapped
member (positional records such as `record Row(int Id, string Name)` work as they are).

## MLFH015

**Annotated member cannot be assigned** (Error)

**Cause:** a member has FileHelpers field attributes, but the generated code cannot assign it: it is
get-only, `readonly`, or its setter is not accessible, and no constructor parameter binds it.
(Unannotated members that cannot be assigned, such as computed properties, are skipped silently.)

```csharp
[DelimitedRecord(",")]
public class Row
{
    [FieldOrder(1)] public int Id { get; }      // MLFH015: get-only, not set by a constructor
}
```

**Fix:** add an accessible setter (or `init`), bind the member through a constructor parameter with the
same name, or mark it `[FieldHidden]`.

## MLFH016

**Generic record types are not supported** (Error)

**Cause:** the record type is generic, or is nested in a generic type. The generator creates one
descriptor per closed type at compile time and cannot describe an open generic type.

```csharp
[DelimitedRecord(",")]
public class Row<T> { public T Value = default!; }   // MLFH016
```

**Fix:** use a non-generic record type, for example one closed type per layout.

## MLFH017

**Record type is not accessible** (Error)

**Cause:** the record type, or a type that contains it, is `private` or `protected`. The descriptor is
generated in a separate file of the same assembly and cannot reference it. (File-local types, declared
with `file`, are skipped without a descriptor.)

```csharp
public class Importer
{
    [DelimitedRecord(",")]
    private class Row { public string A = ""; }    // MLFH017
}
```

**Fix:** make the record type and all containing types `public` or `internal`.

## MLFH018

**Required field after an optional field** (Error)

**Cause:** a field without `[FieldOptional]` follows a `[FieldOptional]` field. Only trailing fields can be
missing from a record (FileHelpers 3.x rejects this layout at run time).

```csharp
[DelimitedRecord(",")]
public class Row
{
    public string A = "";
    [FieldOptional] public string B = "";
    public string C = "";                // MLFH018
}
```

**Fix:** mark every field after the first optional field `[FieldOptional]`, or reorder the fields.

## MLFH019

**`ConverterKind` does not match the field type** (Error)

**Cause:** `[FieldConverter(ConverterKind.X)]` is used on a field of a type that the converter does not
produce. Each `ConverterKind` converts one CLR type: `Date` and `DateMultiFormat` produce `DateTime`,
`Enum` any enum, `Boolean` `bool`, `Guid` `Guid`, and each numeric kind its numeric type
(`PercentDouble` produces `double`).

```csharp
[DelimitedRecord(",")]
public class Row
{
    [FieldConverter(ConverterKind.Date, "yyyyMMdd")]
    public string When = "";             // MLFH019: Date produces DateTime
}
```

**Fix:** use the `ConverterKind` for the field type, or change the field type.

```csharp
[FieldConverter(ConverterKind.Date, "yyyyMMdd")] public DateTime When;
```

## MLFH020

**Descriptor generation failed** (Error)

**Cause:** the generator hit an unexpected internal error while generating the descriptor of one record
type. The message contains the exception type and message. Other record types are still generated.

**Fix:** this is a bug in the generator. Please
[open an issue](https://github.com/ModernLibs/FileHelpers/issues) with the record declaration and the
full diagnostic message. As a workaround, simplify the record (for example remove attributes one at a
time) to find the construct that triggers the failure.

## MLFH021

**Invalid converter arguments** (Error)

**Cause:** a built-in converter gets arguments it can never work with. Currently:
`[FieldConverter(ConverterKind.DateMultiFormat)]` without any date format.

```csharp
[DelimitedRecord(",")]
public class Row
{
    [FieldConverter(ConverterKind.DateMultiFormat)]   // MLFH021: no formats
    public DateTime When;
}
```

**Fix:** pass the formats to try, in order.

```csharp
[FieldConverter(ConverterKind.DateMultiFormat, "yyyy-MM-dd", "dd/MM/yyyy")]
public DateTime When;
```

## MLFH022

**No converter constructor for the converter arguments** (Error)

**Cause:** `[FieldConverter(typeof(X), "arg1", ...)]` passes arguments, but `X` has no public constructor that
takes that many `string` parameters, or a single `params string[]` parameter. The generated code creates the
converter with `new X("arg1", ...)` (no reflection), so a matching constructor is required. FileHelpers 3.x
also passes the arguments to a constructor and fails at run time when none matches.

```csharp
public sealed class UpperConverter : ConverterBase
{
    public override object? StringToField(string text) => text.ToUpperInvariant();
}

[DelimitedRecord(",")]
public class Row
{
    [FieldConverter(typeof(UpperConverter), "en-US")]   // MLFH022: no constructor taking one string
    public string Name = "";
}
```

**Fix:** add a constructor with one `string` parameter per argument (or `params string[]`), or remove the
arguments.

```csharp
public sealed class UpperConverter : ConverterBase
{
    private readonly CultureInfo _culture;
    public UpperConverter(string culture) => _culture = CultureInfo.GetCultureInfo(culture);
    public override object? StringToField(string text) => text.ToUpper(_culture);
}
```

## MLFH023

**Annotated member is not accessible** (Error)

**Cause:** a `private`, `protected` or `private protected` field or property, a write-only property, or
a property whose get accessor is `private` or `protected`, carries FileHelpers field attributes. The
descriptor is generated in another file of the same assembly: it can only read and assign `public`,
`internal` and `protected internal` members, and it formats a property through its get accessor.
(FileHelpers 3.x also maps private fields through reflection.) Unannotated members of this kind are not
part of the layout; those that 3.x maps are reported as [MLFH028](#mlfh028) warnings. `[FieldHidden]`
members are never reported.

```csharp
[DelimitedRecord(",")]
public class Row
{
    public string A = "";
    [FieldOrder(2)] private string _b = "";   // MLFH023
}
```

**Fix:** make the member (and its get accessor) `public` or `internal` (internal members are mapped like
public ones), or remove its FileHelpers attributes.

## MLFH024

**`[FieldOrder]` must be used on all fields or none** (Error)

**Cause:** some fields or properties of the record have `[FieldOrder]` and others do not. The column order
would be a mix of explicit numbers and declaration order. FileHelpers 3.x rejects this layout at run time.
The rule is reported once for every member without `[FieldOrder]`. `[FieldHidden]` members are not part of
the layout and are exempt.

```csharp
[DelimitedRecord(",")]
public class Row
{
    [FieldOrder(2)] public string A = "";
    public string B = "";                // MLFH024
}
```

**Fix:** add `[FieldOrder(N)]` to every mapped member, or remove it from all of them (the declaration order
is then the column order).

## MLFH025

**`[FieldQuoted]` is only valid in delimited records** (Error)

**Cause:** a field of a `[FixedLengthRecord]` type is marked `[FieldQuoted]`. Fixed-length fields are
located by position, so quoting has no meaning. FileHelpers 3.x rejects this at run time.

```csharp
[FixedLengthRecord]
public class Row
{
    [FieldFixedLength(10)] [FieldQuoted] public string Name = "";   // MLFH025
}
```

**Fix:** remove `[FieldQuoted]` (use `[FieldTrim]` to strip padding), or make the record a
`[DelimitedRecord]`.

## MLFH026

**C# 11 or later is required** (Error)

**Cause:** the project compiles with a C# language version older than 11 (for example
`<LangVersion>10</LangVersion>`, or the default language version of an older target framework). The generated
descriptors use C# 11 features (file-local types, `scoped` locals) and would not compile, so the generator
skips them and reports this error once for every record type.

```xml
<PropertyGroup>
  <LangVersion>10</LangVersion>   <!-- MLFH026 on every [DelimitedRecord] / [FixedLengthRecord] type -->
</PropertyGroup>
```

**Fix:** set `<LangVersion>11</LangVersion>` or later, or remove the setting (C# 11 is the default for
`net7.0` and later target frameworks).

## MLFH027

**ref struct records are not supported** (Error)

**Cause:** the record type is a `ref struct`. `FileHelperEngine<T>`, `FileHelperRegistry<T>` and
`IRecordDescriptor<T>` take the record type as a generic type argument, which a `ref struct` cannot be,
so no descriptor is generated.

```csharp
[DelimitedRecord(",")]
public ref struct Row                 // MLFH027
{
    public int Id;
}
```

**Fix:** declare the record as a `class`, `struct` or `record`.

## MLFH028

**Member mapped by FileHelpers 3.x is not part of the layout** (Warning)

**Cause:** an unannotated member that FileHelpers 3.x maps as a column is not part of the generated
layout. 3.x maps every instance field of the record and its base classes through reflection, `private`
and `protected` ones included, and the compiler-generated backing field of every auto-property. The
generated descriptor maps only `public`, `internal` and `protected internal` members that it can read,
so a record migrated from 3.x would silently lose these columns. The warning is reported for:

- `private`, `protected` and `private protected` instance fields (`readonly` ones included);
- `private`, `protected` and `private protected` auto-properties, and auto-properties whose get accessor
  is `private` or `protected` (3.x maps their backing field).

It is not reported for `const` and `static` members, delegate-typed fields and events (3.x skips them),
properties without a backing field (3.x never maps properties), compiler-generated members, and members
marked `[FieldHidden]` or `[FieldNotInFile]`. Annotated members of this kind are errors
([MLFH023](#mlfh023)).

```csharp
[DelimitedRecord(",")]
public class Row
{
    public string Name = "";
    private int _retries;              // MLFH028: a column in 3.x
    public int Retries() => _retries;
}
```

**Fix:** if the member is a column, make it `public` or `internal` (a property also needs an accessible
get accessor). If it is not, mark it `[FieldHidden]` (3.x would also need that to skip it).

## MLFH029

**`[FieldInNewLine]` first field with `[IgnoreEmptyLines]`** (Warning)

**Cause:** the first field of the record is marked `[FieldInNewLine]` and the record has
`[IgnoreEmptyLines]`. With a `[FieldInNewLine]` first field every record is written as a blank line
followed by the fields, and read the same way (as in FileHelpers 3.x). `[IgnoreEmptyLines]` skips that
blank line before the record is read, so the engine reads the record from the wrong lines and cannot read
the files it writes. FileHelpers 3.x behaves the same but does not report it.

```csharp
[DelimitedRecord(",")]
[IgnoreEmptyLines]
public class Row
{
    [FieldInNewLine] public string A = "";   // MLFH029
    public string B = "";
}
```

**Fix:** remove `[FieldInNewLine]` from the first field (put it on the field that really starts a new
line), or remove `[IgnoreEmptyLines]`.
