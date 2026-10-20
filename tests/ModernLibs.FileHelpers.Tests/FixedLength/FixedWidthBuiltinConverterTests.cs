// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types per file.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.FixedLength;

#region Fixtures — one record per logical grouping (widths chosen for golden strings)

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedConvNumericRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(4)]
    [FieldConverter(ConverterKind.Int32)]
    public int Id;

    [FieldOrder(2)]
    [FieldFixedLength(20)]
    [FieldConverter(ConverterKind.Int64)]
    public long Big;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedConvBoolDecimalRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(5)]
    [FieldTrim(TrimMode.Both)]
    [FieldConverter(ConverterKind.Boolean)]
    public bool Flag;

    [FieldOrder(2)]
    [FieldFixedLength(10)]
    [FieldTrim(TrimMode.Both)]
    [FieldConverter(ConverterKind.Decimal)]
    public decimal Amount;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedConvDateGuidCharRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(19)]
    [FieldConverter(ConverterKind.Date)]
    public DateTime When;

    [FieldOrder(2)]
    [FieldFixedLength(36)]
    [FieldConverter(ConverterKind.Guid)]
    public Guid Token;

    [FieldOrder(3)]
    [FieldFixedLength(1)]
    [FieldConverter(ConverterKind.Char)]
    public char Grade;

    [FieldOrder(4)]
    [FieldFixedLength(4)]
    [FieldConverter(ConverterKind.Enum)]
    public FixedConvStatus Status;
}

internal enum FixedConvStatus
{
    New,
    Done,
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedConvDoublePercentRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(8)]
    [FieldConverter(ConverterKind.Double)]
    public double Ratio;

    [FieldOrder(2)]
    [FieldFixedLength(8)]
    [FieldConverter(ConverterKind.PercentDouble)]
    public double Pct;
}

#endregion

/// <summary>Built-in converters on fixed-width fields via <see cref="FileHelperEngine{T}"/>.</summary>
[Category("FixedWidth")]
[Category("Converters")]
public sealed class FixedWidthBuiltinConverterTests
{
    [Test]
    [DisplayName("ReadString fixed-width Int32 and Int64 converters parse padded numerics")]
    public async Task ReadString_FixedWidth_Int32Int64_Converters()
    {
        // Arrange: 4 + 20 = 24 chars
        var engine = new FileHelperEngine<FixedConvNumericRecord>();
        var line = "0042" + "00000000000000000007";

        // Act: Read fixed-width numeric fields through the engine
        var rows = engine.ReadString(line);

        // Assert: Numeric converter values are parsed from their fixed slices
        await Assert.That(rows[0].Id).IsEqualTo(42);
        await Assert.That(rows[0].Big).IsEqualTo(7L);
    }

    [Test]
    [DisplayName("WriteString then ReadString round-trips fixed-width bool and decimal converters")]
    public async Task WriteString_ReadString_FixedWidth_BooleanDecimal_RoundTrip()
    {
        // Arrange
        var engine = new FileHelperEngine<FixedConvBoolDecimalRecord>();
        var original = new FixedConvBoolDecimalRecord { Flag = true, Amount = 12.5m };

        // Act: Write then read the record through fixed-width engine APIs
        var text = engine.WriteString([original]);
        var back = engine.ReadString(text);

        // Assert: Bool and decimal values survive fixed-width formatting
        await Assert.That(back.Length).IsEqualTo(1);
        await Assert.That(back[0].Flag).IsTrue();
        await Assert.That(back[0].Amount).IsEqualTo(12.5m);
    }

    [Test]
    [DisplayName("ReadString fixed-width DateTime Guid char enum converters parse golden layout")]
    public async Task ReadString_FixedWidth_DateTimeGuidCharEnum_Converters()
    {
        // Arrange: 19 (3.x default "ddMMyyyy", blank-padded) + 36 + 1 + 4 = 60
        var engine = new FileHelperEngine<FixedConvDateGuidCharRecord>();
        var g = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");
        var line = "27012026           " + "11223344-5566-7788-99aa-bbccddeeff00" + "A" + "Done";

        // Act: Read fixed-width date, Guid, char, and enum fields
        var rows = engine.ReadString(line);

        // Assert: Each built-in converter parses its fixed slice
        await Assert.That(rows[0].When.Year).IsEqualTo(2026);
        await Assert.That(rows[0].When.Month).IsEqualTo(1);
        await Assert.That(rows[0].When.Day).IsEqualTo(27);
        await Assert.That(rows[0].Token).IsEqualTo(g);
        await Assert.That(rows[0].Grade).IsEqualTo('A');
        await Assert.That(rows[0].Status).IsEqualTo(FixedConvStatus.Done);
    }

    [Test]
    [DisplayName("ReadString fixed-width Double and PercentDouble converters parse values")]
    public async Task ReadString_FixedWidth_DoublePercent_Converters()
    {
        // Arrange: 8 + 8 = 16 — ratio 1.25, percent 12.5
        var engine = new FileHelperEngine<FixedConvDoublePercentRecord>();
        var line = "    1.25" + "  12.5  ";

        // Act: Read fixed-width double and percent fields
        var rows = engine.ReadString(line);

        // Assert: Double and percent converters parse invariant numeric values
        await Assert.That(rows[0].Ratio).IsEqualTo(1.25);
        // PercentDoubleConverter parses like DoubleConverter, as in FileHelpers 3.5.2 (see ConverterTests).
        await Assert.That(rows[0].Pct).IsEqualTo(12.5);
    }

    [Test]
    [DisplayName("ReadString fixed-width PercentDouble field with '%' raises ConvertException like FileHelpers 3.5.2")]
    public async Task ReadString_FixedWidth_PercentSign_Throws()
    {
        // Arrange: 8 + 8 = 16 — the percent field contains '%'
        var engine = new FileHelperEngine<FixedConvDoublePercentRecord>();
        var line = "    1.25" + "  12.5% ";

        // Act + Assert: 3.5.2 rejects any value containing '%'
        await Assert.That(() => engine.ReadString(line)).Throws<ConvertException>();
    }
}
