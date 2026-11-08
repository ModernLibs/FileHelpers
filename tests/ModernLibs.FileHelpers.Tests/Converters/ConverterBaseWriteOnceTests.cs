// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>A legacy converter that writes a long text and counts its calls.</summary>
public sealed class CbwoLongConverter : ConverterBase
{
    private static int s_calls;

    public static int Calls
    {
        get => Volatile.Read(ref s_calls);
        set => Volatile.Write(ref s_calls, value);
    }

    public override object? StringToField(string text) => text.Length;

    public override string? FieldToString(object? field)
    {
        Interlocked.Increment(ref s_calls);
        return field is int length ? new string('x', length) : null;
    }
}

[DelimitedRecord(",")]
public sealed class CbwoRecord
{
    [FieldOrder(1)] public int Id { get; set; }

    [FieldOrder(2)]
    [FieldConverter(typeof(CbwoLongConverter))]
    public int Length { get; set; }
}

/// <summary>
/// A <see cref="ConverterBase"/> is called once per written field, also when its text does not fit the first
/// formatting buffer (the write retries with a larger one).
/// </summary>
[Category("Converters")]
public sealed class ConverterBaseWriteOnceTests
{
    [Test]
    [NotInParallel(nameof(CbwoLongConverter))]
    [DisplayName("FieldToString runs once per record, whatever the length of its text")]
    public async Task FieldToString_RunsOncePerField()
    {
        // Arrange: texts from empty to 100K characters
        var engine = new FileHelperEngine<CbwoRecord>();
        int[] lengths = [0, 1, 63, 64, 65, 200, 1_000, 5_000, 100_000];
        var records = lengths.Select((length, i) => new CbwoRecord { Id = i, Length = length }).ToArray();
        CbwoLongConverter.Calls = 0;

        // Act
        string text = engine.WriteString(records);

        // Assert: one call per record and the full text of each
        await Assert.That(CbwoLongConverter.Calls).IsEqualTo(lengths.Length);
        string expected = string.Concat(lengths.Select((length, i) => $"{i},{new string('x', length)}\r\n"));
        await Assert.That(text).IsEqualTo(expected);
    }

    [Test]
    [DisplayName("The adapter writes a null text as empty and still reports a destination that is too small")]
    public async Task Adapter_NullAndTooSmall()
    {
        // Arrange
        var adapter = new ConverterBaseAdapter<int>(new CbwoLongConverter());
        var small = new char[4];

        // Act
        bool fits = adapter.TryFormat(3, small, out int fitsWritten);
        bool tooSmall = adapter.TryFormat(10, small, out int tooSmallWritten);
        var large = new char[16];
        bool retried = adapter.TryFormat(10, large, out int retriedWritten);

        // Assert
        await Assert.That((fits, fitsWritten)).IsEqualTo((true, 3));
        await Assert.That((tooSmall, tooSmallWritten)).IsEqualTo((false, 0));
        await Assert.That((retried, retriedWritten)).IsEqualTo((true, 10));
        await Assert.That(new string(large, 0, 10)).IsEqualTo("xxxxxxxxxx");
    }
}
