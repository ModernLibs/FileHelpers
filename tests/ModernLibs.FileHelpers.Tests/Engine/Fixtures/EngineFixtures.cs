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
/// Simple delimited fixture used by engine read/write tests (ENGI-01..ENGI-07, ENGI-16).
/// Header-skipping uses [IgnoreFirst(1)] to exercise D-10 HeaderText capture.
/// Category tag: Phase07.
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
/// IN-01: Positional record struct fixture. Exercises the generator's positional-constructor
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
/// Fixture exercising [FieldTrim] (D-05). The generator is expected to emit inline trim logic
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
/// Fixture exercising [FieldQuoted] (D-06). The generator is expected to emit quote wrapping
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
/// Fixture exercising WR-04: a [FieldQuoted] field with embedded delimiters and a trailing
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
/// Fixture exercising [IgnoreCommentedLines] and [IgnoreEmptyLines] (D-08 filtering).
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
/// Fixture exercising [IgnoreLast(1)] (D-10 FooterText capture on read).
/// </summary>
[DelimitedRecord(",")]
[IgnoreLast(1)]
internal sealed class FooteredRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;
}
