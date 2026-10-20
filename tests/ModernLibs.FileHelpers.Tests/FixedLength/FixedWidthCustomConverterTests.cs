// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture + nested converter types in one file.
#pragma warning disable MA0048

using System.Globalization;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.FixedLength;

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedCustomSpanRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(6)]
    [FieldConverter(typeof(FixedWidthCustomConverterTests.CodeTokenConverter))]
    public FixedWidthCustomConverterTests.CodeToken Code;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedCustomConverterBaseRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(4)]
    [FieldConverter(typeof(FixedWidthCustomConverterTests.TimesTenConverterBase))]
    public int Value;
}

/// <summary>Custom ISpanConverter and ConverterBase on fixed-width fields.</summary>
[Category("FixedWidth")]
public sealed class FixedWidthCustomConverterTests
{
    public readonly record struct CodeToken(int Value);

    /// <summary>Parses <c>x&lt;n&gt;</c> spans into <see cref="CodeToken"/> values.</summary>
    public struct CodeTokenConverter : ISpanConverter<CodeToken>
    {
        public CodeTokenConverter() { }

        public bool TryParse(ReadOnlySpan<char> span, out CodeToken value)
        {
            value = default;
            ReadOnlySpan<char> trimmed = span.Trim();
            if (trimmed.Length < 3 || trimmed[0] != 'x')
                return false;

            int close = trimmed.IndexOf('>');
            if (close <= 1)
                return false;

            bool parsed = int.TryParse(trimmed[1..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number);
            value = new CodeToken(number);
            return parsed;
        }

        public bool TryFormat(CodeToken value, Span<char> destination, out int charsWritten)
        {
            string formatted = string.Create(CultureInfo.InvariantCulture, $"x{value.Value}>");
            if (formatted.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            formatted.AsSpan().CopyTo(destination);
            charsWritten = formatted.Length;
            return true;
        }
    }

    public sealed class TimesTenConverterBase : ConverterBase
    {
        public override object? StringToField(string text)
            => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value * 10 : 0;

        public override string? FieldToString(object? field) =>
            field is int value ? (value / 10).ToString(CultureInfo.InvariantCulture) : "0";
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldConverter ISpanConverter parses custom span format")]
    public async Task ReadString_FixedWidth_CustomISpanConverter_Parses()
    {
        // Arrange: width 6 holds "x99>"
        var engine = new FileHelperEngine<FixedCustomSpanRecord>();

        // Act: Read a custom span-converted fixed-width field
        var rows = engine.ReadString("x99>  ");

        // Assert: The custom value type is parsed from the fixed slice
        await Assert.That(rows[0].Code.Value).IsEqualTo(99);
    }

    [Test]
    [DisplayName("WriteString fixed-width FieldConverter ISpanConverter emits custom format")]
    public async Task WriteString_FixedWidth_CustomISpanConverter_Formats()
    {
        // Arrange: Create an engine for the custom span-converted record
        var engine = new FileHelperEngine<FixedCustomSpanRecord>();

        // Act: Format a custom value type through the fixed-width write path
        var text = engine.WriteString([new FixedCustomSpanRecord { Code = new CodeToken(5) }]);

        // Assert: contains custom prefix/suffix inside fixed width
        await Assert.That(text.TrimEnd().Contains("x5>", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    [DisplayName("ReadString fixed-width ConverterBase-backed field parses via adapter")]
    public async Task ReadString_FixedWidth_ConverterBaseAdapter_Parses()
    {
        // Arrange: Stored value 70; ConverterBase sees "7"
        var engine = new FileHelperEngine<FixedCustomConverterBaseRecord>();

        // Act: Read a ConverterBase-backed fixed-width field
        var rows = engine.ReadString("7   ");

        // Assert: The adapter delegates parsing to the legacy converter
        await Assert.That(rows[0].Value).IsEqualTo(70);
    }

    [Test]
    [DisplayName("WriteString fixed-width ConverterBase-backed field formats via adapter")]
    public async Task WriteString_FixedWidth_ConverterBaseAdapter_Formats()
    {
        // Arrange: Create an engine for the ConverterBase-backed record
        var engine = new FileHelperEngine<FixedCustomConverterBaseRecord>();

        // Act: Format through the legacy ConverterBase adapter
        var text = engine.WriteString([new FixedCustomConverterBaseRecord { Value = 80 }]);

        // Assert: FieldToString receives 80 → "8"
        await Assert.That(text.TrimEnd().StartsWith('8')).IsTrue();
    }
}
