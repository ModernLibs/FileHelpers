// MA0048: Fixture types grouped in one file.
#pragma warning disable MA0048

using System.Globalization;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Tests.Compat;

// The record types below carry their attributes, so the current generator makes their descriptors. The
// 4.9.1 generator compiled this same file in a scratch project: its output for each `...Twin` type, renamed
// to the type without the suffix, is checked in under Generated49 (see Generated49/README.md). The plain
// types in Compat49Records.cs have the same members in the same order and no record attributes.
// The custom converters are shared by both sets: generated code names them by their full type name.

/// <summary>The 25 columns of the benchmark <c>PackageAsset</c>: strings, ints, a long, bools, two <c>s</c> dates.</summary>
[DelimitedRecord(",")]
[IgnoreFirst]
public sealed class Compat49Plain25Twin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Version { get; set; } = string.Empty;
    [FieldOrder(3)] public string Authors { get; set; } = string.Empty;
    [FieldOrder(4)] public string Description { get; set; } = string.Empty;
    [FieldOrder(5)] public string Tags { get; set; } = string.Empty;
    [FieldOrder(6)] public int DownloadCount { get; set; }
    [FieldOrder(7)] public string ProjectUrl { get; set; } = string.Empty;
    [FieldOrder(8)] public string LicenseUrl { get; set; } = string.Empty;
    [FieldOrder(9)] public string IconUrl { get; set; } = string.Empty;
    [FieldOrder(10)] public string Language { get; set; } = string.Empty;
    [FieldOrder(11)] public string Framework { get; set; } = string.Empty;
    [FieldOrder(12)] public string MinClientVersion { get; set; } = string.Empty;
    [FieldOrder(13)] public int Type { get; set; }
    [FieldOrder(14)] public bool IsPrerelease { get; set; }
    [FieldOrder(15), FieldConverter(ConverterKind.Date, "s")] public DateTime Published { get; set; }
    [FieldOrder(16), FieldConverter(ConverterKind.Date, "s")] public DateTime LastUpdated { get; set; }
    [FieldOrder(17)] public long PackageSize { get; set; }
    [FieldOrder(18)] public int DependencyCount { get; set; }
    [FieldOrder(19)] public bool HasReadme { get; set; }
    [FieldOrder(20)] public bool HasLicense { get; set; }
    [FieldOrder(21)] public bool HasIcon { get; set; }
    [FieldOrder(22)] public bool IsSigned { get; set; }
    [FieldOrder(23)] public string RepositoryType { get; set; } = string.Empty;
    [FieldOrder(24)] public string RepositoryUrl { get; set; } = string.Empty;
    [FieldOrder(25)] public bool RequireLicenseAcceptance { get; set; }
}

/// <summary>Two trailing <c>[FieldOptional]</c> fields and a footer line.</summary>
[DelimitedRecord(",")]
[IgnoreLast]
public sealed class Compat49OptionalTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldOptional] public string? Note { get; set; }
    [FieldOrder(4), FieldOptional] public int? Extra { get; set; }
}

/// <summary>A one-char and a two-char <c>[FieldDelimiter]</c> before the record delimiter.</summary>
[DelimitedRecord(",")]
public sealed class Compat49PerFieldDelimiterTwin
{
    [FieldOrder(1), FieldDelimiter("|")] public int Id { get; set; }
    [FieldOrder(2), FieldDelimiter("::")] public string Code { get; set; } = string.Empty;
    [FieldOrder(3), FieldConverter(ConverterKind.Decimal, ".")] public decimal Amount { get; set; }
    [FieldOrder(4)] public string Tail { get; set; } = string.Empty;
}

/// <summary>A <c>[FieldInNewLine]</c> field after a quoted field: two physical lines per record.</summary>
[DelimitedRecord(",")]
public sealed class Compat49NewLineTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldInNewLine] public string Street { get; set; } = string.Empty;
    [FieldOrder(4)] public int Zip { get; set; }
}

/// <summary><c>[FieldTrim(TrimMode.Both, 'x', ' ')]</c> on a quoted and on a plain field.</summary>
[DelimitedRecord(",")]
public sealed class Compat49TrimTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted, FieldTrim(TrimMode.Both, 'x', ' ')] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldTrim(TrimMode.Both, 'x', ' ')] public string Code { get; set; } = string.Empty;
    [FieldOrder(4)] public int Count { get; set; }
}

/// <summary><c>QuoteMode.AlwaysQuoted</c>.</summary>
[DelimitedRecord(",")]
public sealed class Compat49QuoteAlwaysTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted('"', QuoteMode.AlwaysQuoted)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;
}

/// <summary><c>QuoteMode.OptionalForRead</c>.</summary>
[DelimitedRecord(",")]
public sealed class Compat49QuoteOptionalReadTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted('"', QuoteMode.OptionalForRead)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;
}

/// <summary><c>QuoteMode.OptionalForWrite</c>.</summary>
[DelimitedRecord(",")]
public sealed class Compat49QuoteOptionalWriteTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted('"', QuoteMode.OptionalForWrite)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;
}

/// <summary><c>QuoteMode.OptionalForBoth</c>.</summary>
[DelimitedRecord(",")]
public sealed class Compat49QuoteOptionalBothTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted('"', QuoteMode.OptionalForBoth)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;
}

/// <summary><c>MultilineMode.NotAllow</c>: a line break in the value fails on read and on write.</summary>
[DelimitedRecord(",")]
public sealed class Compat49MultilineNotAllowTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.NotAllow)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;
}

/// <summary>A class with setters only: its descriptor implements <c>IRecyclableDescriptor&lt;T&gt;</c>.</summary>
[DelimitedRecord(",")]
public sealed class Compat49RecyclableTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public decimal? Amount { get; set; }
    [FieldOrder(4)] public bool Flag { get; set; }
}

/// <summary>A delimited struct record that ignores empty lines.</summary>
[DelimitedRecord(";")]
[IgnoreEmptyLines]
public struct Compat49StructTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; }
    [FieldOrder(3)] public DateOnly Day { get; set; }
    [FieldOrder(4)] public double Ratio { get; set; }
}

/// <summary>Read-only properties set by a constructor; commented lines are skipped.</summary>
[DelimitedRecord(",")]
[IgnoreCommentedLines('#')]
public sealed class Compat49CtorTwin
{
    public Compat49CtorTwin(int id, string name, DateTime day)
    {
        Id = id;
        Name = name;
        Day = day;
    }

    [FieldOrder(1)] public int Id { get; }
    [FieldOrder(2)] public string Name { get; }
    [FieldOrder(3), FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime Day { get; }
}

/// <summary><c>FixedMode.ExactLength</c>, 19 chars.</summary>
[FixedLengthRecord(FixedMode.ExactLength)]
public sealed class Compat49FixedExactTwin
{
    [FieldOrder(1), FieldFixedLength(5), FieldAlign(AlignMode.Right, '0')] public int Id { get; set; }
    [FieldOrder(2), FieldFixedLength(8), FieldTrim(TrimMode.Right)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldFixedLength(6), FieldAlign(AlignMode.Right), FieldConverter(ConverterKind.Decimal, ".")] public decimal Amount { get; set; }
}

/// <summary><c>FixedMode.AllowMoreChars</c>, 19 chars.</summary>
[FixedLengthRecord(FixedMode.AllowMoreChars)]
public sealed class Compat49FixedMoreTwin
{
    [FieldOrder(1), FieldFixedLength(5), FieldAlign(AlignMode.Right, '0')] public int Id { get; set; }
    [FieldOrder(2), FieldFixedLength(8), FieldTrim(TrimMode.Right)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldFixedLength(6), FieldAlign(AlignMode.Right), FieldConverter(ConverterKind.Decimal, ".")] public decimal Amount { get; set; }
}

/// <summary><c>FixedMode.AllowLessChars</c>, 19 chars.</summary>
[FixedLengthRecord(FixedMode.AllowLessChars)]
public sealed class Compat49FixedLessTwin
{
    [FieldOrder(1), FieldFixedLength(5), FieldAlign(AlignMode.Right, '0')] public int Id { get; set; }
    [FieldOrder(2), FieldFixedLength(8), FieldTrim(TrimMode.Right)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldFixedLength(6), FieldAlign(AlignMode.Right), FieldConverter(ConverterKind.Decimal, ".")] public decimal? Amount { get; set; }
}

/// <summary><c>FixedMode.AllowVariableLength</c>, 19 chars.</summary>
[FixedLengthRecord(FixedMode.AllowVariableLength)]
public sealed class Compat49FixedVariableTwin
{
    [FieldOrder(1), FieldFixedLength(5), FieldAlign(AlignMode.Right, '0')] public int Id { get; set; }
    [FieldOrder(2), FieldFixedLength(8), FieldTrim(TrimMode.Right)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldFixedLength(6), FieldAlign(AlignMode.Right), FieldConverter(ConverterKind.Decimal, ".")] public decimal? Amount { get; set; }
}

/// <summary>Three <c>ConverterBase</c> converters: one throwing <c>ConvertException</c>, one with
/// <c>CustomNullHandling</c>, one with a constructor argument.</summary>
[DelimitedRecord(",")]
public sealed class Compat49ConverterBaseTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldConverter(typeof(Compat49UpperConverter))] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldConverter(typeof(Compat49HexConverter))] public int Hex { get; set; }
    [FieldOrder(4), FieldConverter(typeof(Compat49PrefixConverter), "p-")] public string? Code { get; set; }
}

/// <summary>A direct <c>ISpanConverter&lt;decimal&gt;</c> on a <c>decimal</c> and on a <c>decimal?</c> field.</summary>
[DelimitedRecord(",")]
public sealed class Compat49SpanConverterTwin
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldConverter(typeof(Compat49CentsConverter))] public decimal Price { get; set; }
    [FieldOrder(3), FieldConverter(typeof(Compat49CentsConverter))] public decimal? Discount { get; set; }
}

/// <summary>Upper-cases on read, lower-cases on write; a value starting with '!' raises <see cref="ConvertException"/>.</summary>
public sealed class Compat49UpperConverter : ConverterBase
{
    public override object? StringToField(string text) =>
        text.StartsWith('!') ? throw new ConvertException(text, typeof(string), "Values must not start with '!'.") : text.ToUpperInvariant();

    public override string? FieldToString(object? field) => ((string?)field)?.ToLowerInvariant();
}

/// <summary>Hexadecimal <see cref="int"/>; reads an empty value as -1 itself (<c>CustomNullHandling</c>).</summary>
public sealed class Compat49HexConverter : ConverterBase
{
    // `protected internal`: the library grants this test assembly InternalsVisibleTo. The scratch project that
    // ran the 4.9.1 generator has no such access and compiled this line as `protected override`.
    protected internal override bool CustomNullHandling => true;

    public override object? StringToField(string text) =>
        string.IsNullOrWhiteSpace(text) ? -1 : int.Parse(text.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public override string? FieldToString(object? field) => ((int)field!).ToString("X", CultureInfo.InvariantCulture);
}

/// <summary>Strips a prefix given as the converter argument on read and adds it on write.</summary>
public sealed class Compat49PrefixConverter : ConverterBase
{
    private readonly string _prefix;

    public Compat49PrefixConverter(string prefix) => _prefix = prefix;

    public override object? StringToField(string text) =>
        text.StartsWith(_prefix, StringComparison.Ordinal) ? text[_prefix.Length..] : throw new FormatException("Missing prefix " + _prefix + ".");

    public override string? FieldToString(object? field) => _prefix + (string?)field;
}

/// <summary>Whole cents to <see cref="decimal"/>; a value starting with '!' throws <see cref="InvalidOperationException"/>.</summary>
public sealed class Compat49CentsConverter : ISpanConverter<decimal>
{
    public bool TryParse(ReadOnlySpan<char> span, out decimal value)
    {
        if (span.StartsWith("!"))
            throw new InvalidOperationException("Cents must not start with '!'.");
        bool ok = long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long cents);
        value = cents / 100m;
        return ok;
    }

    public bool TryFormat(decimal value, Span<char> destination, out int charsWritten) =>
        ((long)(value * 100m)).TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}
