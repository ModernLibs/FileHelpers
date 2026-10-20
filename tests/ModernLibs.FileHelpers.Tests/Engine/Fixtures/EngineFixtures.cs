// CA1707: Test fixture identifiers use underscores for readability.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not applicable to fixture declarations.
#pragma warning disable MA0004
// MA0025: Fixture bodies intentionally empty — engine populates fields via source-generated code.
#pragma warning disable MA0025
// MA0048: Multiple fixture types per file — intentional; groups related engine fixtures.
#pragma warning disable MA0048
// CS0649: Public fields/properties are assigned by the source-generated descriptor, not in user code.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Engine.Fixtures;

/// <summary>
/// Simple delimited fixture used by engine read/write tests.
/// Header-skipping uses [IgnoreFirst(1)] to exercise HeaderText capture.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class CsvOrder
{
    [FieldOrder(1)] public int OrderId;
    [FieldOrder(2)] public string CustomerName = string.Empty;
    [FieldOrder(3)] public decimal Amount;
}

/// <summary>
/// Positional record struct fixture. Exercises the generator's positional-constructor
/// code path (all init-only + struct → <c>new T(__v0, __v1, __v2)</c>) end-to-end through
/// <see cref="FileHelperEngine{T}"/>. Prior fixtures used classes only; this guards against
/// regressions in the positional-ctor emission path at the engine level.
/// </summary>
[DelimitedRecord(",")]
internal readonly record struct PositionalOrder(
    [property: FieldOrder(1)] int OrderId,
    [property: FieldOrder(2)] string CustomerName,
    [property: FieldOrder(3)] decimal Amount);

/// <summary>
/// Fixed-length fixture used by ReadFile/ReadString fixed-length tests.
/// Total width: 6 + 20 + 10 = 36 chars per record.
/// </summary>
[FixedLengthRecord]
internal sealed class FixedEmployee
{
    [FieldOrder(1)] [FieldFixedLength(6)] public int Id;
    [FieldOrder(2)] [FieldFixedLength(20)] public string Name = string.Empty;
    [FieldOrder(3)] [FieldFixedLength(10)] public decimal Salary;
}

/// <summary>
/// Fixture exercising [FieldTrim]. The generator is expected to emit inline trim logic
/// in the Parse body for fields with TrimMode != None.
/// </summary>
[DelimitedRecord(",")]
internal sealed class TrimmedRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    public string Name = string.Empty;
}

/// <summary>
/// Fixture exercising [FieldQuoted]. The generator is expected to emit quote wrapping
/// in the Format body and quote stripping in the Parse body.
/// </summary>
[DelimitedRecord(",")]
internal sealed class QuotedRecord
{
    [FieldOrder(1)]
    [FieldQuoted]
    public string Description = string.Empty;
}

/// <summary>
/// Fixture exercising a [FieldQuoted] field with embedded delimiters and a trailing
/// non-quoted field. Parse must tolerate commas inside the quoted payload without splitting.
/// </summary>
[DelimitedRecord(",")]
internal sealed class QuotedWithTrailing
{
    [FieldOrder(1)]
    [FieldQuoted]
    public string Description = string.Empty;

    [FieldOrder(2)] public int Count;
}

/// <summary>
/// Fixture exercising [IgnoreCommentedLines] and [IgnoreEmptyLines] (record filtering).
/// </summary>
[DelimitedRecord(",")]
[IgnoreCommentedLines('#')]
[IgnoreEmptyLines]
internal sealed class CommentedRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Value = string.Empty;
}

/// <summary>
/// Fixture exercising [IgnoreLast(1)] (FooterText capture on read).
/// </summary>
[DelimitedRecord(",")]
[IgnoreLast(1)]
internal sealed class FooteredRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;
}

/// <summary>
/// Fixture exercising [FieldCaption] attribute.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class CaptionedRecord
{
    [FieldOrder(1)]
    [FieldCaption("Order ID")]
    public int Id;

    [FieldOrder(2)]
    [FieldCaption("Customer Name")]
    public string Name = string.Empty;

    [FieldOrder(3)]
    public decimal Amount;
}

// Base record for IgnoreInheritedClass tests
internal class IgnoreInheritedBase
{
    public int Id;
    public string BaseField = string.Empty;
}

/// <summary>
/// Derived record WITHOUT IgnoreInheritedClass — should include base fields.
/// </summary>
[DelimitedRecord(",")]
internal sealed class DerivedWithInherited : IgnoreInheritedBase
{
    // base-class fields come first (FileHelpers 3.x order), so no [FieldOrder] is needed.
    public string DerivedField = string.Empty;
}

/// <summary>
/// Derived record WITH IgnoreInheritedClass — should exclude base fields.
/// </summary>
[DelimitedRecord(",")]
[IgnoreInheritedClass]
internal sealed class DerivedWithoutInherited : IgnoreInheritedBase
{
    [FieldOrder(1)]
    public string DerivedField = string.Empty;
}

/// <summary>
/// European decimal parsing with fr-FR culture (comma as decimal separator).
/// Uses semicolon delimiter to avoid conflict with European decimal comma.
/// </summary>
[DelimitedRecord(";")]
internal sealed class EuropeanDecimalRecord
{
    [FieldOrder(0)]
    [FieldConverter(ConverterKind.Decimal, CultureName = "fr-FR")]
    public decimal Amount;

    [FieldOrder(1)]
    public string Name = string.Empty;
}

/// <summary>
/// Date parsing with de-DE culture (dd.MM.yyyy format).
/// Uses semicolon delimiter to avoid conflict with date format dots.
/// </summary>
[DelimitedRecord(";")]
internal sealed class GermanDateRecord
{
    [FieldOrder(0)]
    public int Id;

    [FieldOrder(1)]
    [FieldConverter(ConverterKind.Date, "dd.MM.yyyy", CultureName = "de-DE")]
    public DateTime Date;

    [FieldOrder(2)]
    public string Description = string.Empty;
}

/// <summary>
/// InvariantCulture default (no culture specified) — regression test.
/// Uses semicolon delimiter to allow testing comma rejection.
/// </summary>
[DelimitedRecord(";")]
internal sealed class InvariantDecimalRecord
{
    [FieldOrder(0)]
    [FieldConverter(ConverterKind.Decimal)]
    public decimal Amount;

    [FieldOrder(1)]
    public string Name = string.Empty;
}

/// <summary>
/// Multiple cultures in same record.
/// Uses semicolon delimiter to avoid conflict with European decimal comma.
/// </summary>
[DelimitedRecord(";")]
internal sealed class MultiCultureRecord
{
    [FieldOrder(0)]
    [FieldConverter(ConverterKind.Decimal, CultureName = "fr-FR")]
    public decimal FrenchAmount;

    [FieldOrder(1)]
    [FieldConverter(ConverterKind.Decimal, CultureName = "de-DE")]
    public decimal GermanAmount;

    [FieldOrder(2)]
    [FieldConverter(ConverterKind.Decimal)]
    public decimal InvariantAmount;
}

/// <summary>
/// Fixed-length record with culture.
/// </summary>
[FixedLengthRecord]
internal sealed class FixedCultureRecord
{
    [FieldOrder(0)]
    [FieldFixedLength(10)]
    [FieldConverter(ConverterKind.Decimal, CultureName = "fr-FR")]
    public decimal Amount;

    [FieldOrder(1)]
    [FieldFixedLength(20)]
    public string Name = string.Empty;
}

/// <summary>
/// Double with culture for high-precision numeric parsing.
/// Uses semicolon delimiter to avoid conflict with European decimal comma.
/// </summary>
[DelimitedRecord(";")]
internal sealed class CultureDoubleRecord
{
    [FieldOrder(0)]
    [FieldConverter(ConverterKind.Double, CultureName = "fr-FR")]
    public double Value;

    [FieldOrder(1)]
    public string Label = string.Empty;
}

/// <summary>
/// Master record for CommonSelector testing.
/// RecordType field contains "ORD" prefix for selector classification.
/// </summary>
[DelimitedRecord(",")]
internal sealed class OrderHeader
{
    [FieldOrder(0)]
    public string RecordType = string.Empty;

    [FieldOrder(1)]
    public int OrderId;

    [FieldOrder(2)]
    public string CustomerName = string.Empty;
}

/// <summary>
/// Detail record for CommonSelector testing.
/// RecordType field contains "LIN" prefix for selector classification.
/// </summary>
[DelimitedRecord(",")]
internal sealed class OrderLine
{
    [FieldOrder(0)]
    public string RecordType = string.Empty;

    [FieldOrder(1)]
    public int LineNumber;

    [FieldOrder(2)]
    public string Product = string.Empty;

    [FieldOrder(3)]
    public decimal Price;
}

/// <summary>
/// Simple record for ReadRemaining testing.
/// </summary>
[DelimitedRecord(",")]
internal sealed class SimpleRecord
{
    [FieldOrder(0)]
    public int Id;

    [FieldOrder(1)]
    public string Name = string.Empty;
}

/// <summary>
/// Record with IgnoreEmptyLines for filter testing.
/// </summary>
[DelimitedRecord(",")]
[IgnoreEmptyLines]
internal sealed class SimpleWithIgnoreEmpty
{
    [FieldOrder(0)]
    public int Id;

    [FieldOrder(1)]
    public string Name = string.Empty;
}
