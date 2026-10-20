// MA0048: Multiple fixture types per file — intentional.
#pragma warning disable MA0048
// CA1051: Public fields are the FileHelpers record mapping surface.
#pragma warning disable CA1051
// CS0649: Public fields assigned by source-generated descriptor.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>Delimited class record used by the conversion-error tests.</summary>
[DelimitedRecord(",")]
public sealed class ConvClassRecord
{
    public int Id;
    public string Code = "";
    public int Qty;
}

/// <summary>Delimited struct record used by the conversion-error tests (struct streaming paths).</summary>
[DelimitedRecord(",")]
public struct ConvStructRecord
{
    public int Id;
    public string Code;
    public int Qty;
}

/// <summary>Fixed-length record used by the conversion-error tests.</summary>
[FixedLengthRecord]
public sealed class ConvFixedRecord
{
    [FieldFixedLength(5)] public int Id;
    [FieldFixedLength(3)] public string Code = "";
}

/// <summary>Covers the implicit DateTime, decimal and bool conversion paths.</summary>
[DelimitedRecord(";")]
public sealed class ConvMixedRecord
{
    public decimal Amount;
    public DateTime When;
    public bool Flag;
}

/// <summary>Covers the explicit built-in converter path.</summary>
[DelimitedRecord(",")]
public sealed class ConvBuiltInConverterRecord
{
    [FieldConverter(ConverterKind.Int32)] public int Value;
    [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime Day;
}

/// <summary>Custom 3.x converter that throws a plain exception for "boom".</summary>
public sealed class ThrowingConverter : ConverterBase
{
    public override object? StringToField(string text) =>
        string.Equals(text, "boom", StringComparison.Ordinal)
            ? throw new InvalidOperationException("kaboom")
            : int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    public override string? FieldToString(object? field) => field?.ToString();
}

/// <summary>Custom 3.x converter that reports failures with <see cref="ConvertException"/>.</summary>
public sealed class ConvertExceptionConverter : ConverterBase
{
    public override object? StringToField(string text) =>
        int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v)
            ? v
            : throw new ConvertException(text, typeof(int), "Not a number.");

    public override string? FieldToString(object? field) => field?.ToString();
}

/// <summary>Custom converter fixture.</summary>
[DelimitedRecord(",")]
public sealed class ConvCustomConverterRecord
{
    public string Name = "";
    [FieldConverter(typeof(ThrowingConverter))] public int Plain;
    [FieldConverter(typeof(ConvertExceptionConverter))] public int Strict;
}
