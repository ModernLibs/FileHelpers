// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048
// CA1008, CA1028, CA1069: the fixture enums deliberately have no zero value, non-int storage and an alias.
#pragma warning disable CA1008, CA1028, CA1069

using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

public enum EcColour
{
    Red = 1,
    Green = 2,
    Crimson = Red,
}

[Flags]
public enum EcAccess
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
}

public enum EcSigned : sbyte
{
    Low = -100,
    Zero = 0,
}

public enum EcWide : ulong
{
    Max = ulong.MaxValue,
}

/// <summary>
/// <see cref="EnumConverter{TEnum}"/> writes exactly what <see cref="Enum.ToString()"/> returns, as 4.8.0 did,
/// without allocating.
/// </summary>
[Category("Converters")]
public sealed class EnumConverterTests
{
    private static async Task AssertFormatsAsToString<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var buffer = new char[64];
        bool ok = EnumConverter<TEnum>.Instance.TryFormat(value, buffer, out int written);
        await Assert.That(ok).IsTrue();
        await Assert.That(new string(buffer, 0, written)).IsEqualTo(value.ToString());
    }

    [Test]
    [DisplayName("Enum fields are written as Enum.ToString: names, aliases, flag combinations and undefined values")]
    public async Task TryFormat_MatchesToString()
    {
        // Arrange, Act and Assert: each shape of value
        await AssertFormatsAsToString(EcColour.Red);
        await AssertFormatsAsToString(EcColour.Crimson);
        await AssertFormatsAsToString((EcColour)7);
        await AssertFormatsAsToString(EcAccess.None);
        await AssertFormatsAsToString(EcAccess.Read | EcAccess.Execute);
        await AssertFormatsAsToString((EcAccess)8);
        await AssertFormatsAsToString((EcAccess)9);
        await AssertFormatsAsToString(EcSigned.Low);
        await AssertFormatsAsToString((EcSigned)(-5));
        await AssertFormatsAsToString(EcWide.Max);
        await AssertFormatsAsToString((EcWide)12);
    }

    [Test]
    [DisplayName("Enum TryFormat: a destination too small returns false with nothing written; an exact fit succeeds")]
    public async Task TryFormat_DestinationSize()
    {
        // Arrange: "Read, Execute" is 13 characters
        var value = EcAccess.Read | EcAccess.Execute;
        var small = new char[12];
        var exact = new char[13];

        // Act
        bool smallOk = EnumConverter<EcAccess>.Instance.TryFormat(value, small, out int smallWritten);
        bool exactOk = EnumConverter<EcAccess>.Instance.TryFormat(value, exact, out int exactWritten);

        // Assert
        await Assert.That(smallOk).IsFalse();
        await Assert.That(smallWritten).IsEqualTo(0);
        await Assert.That(exactOk).IsTrue();
        await Assert.That(new string(exact, 0, exactWritten)).IsEqualTo("Read, Execute");
    }

    [Test]
    [DisplayName("Enum TryFormat does not allocate, also for flag combinations and undefined values")]
    public async Task TryFormat_DoesNotAllocate()
    {
        // Arrange: warm up
        var converter = EnumConverter<EcAccess>.Instance;
        var buffer = new char[64];
        var combination = EcAccess.Read | EcAccess.Write;
        var undefined = (EcAccess)8;
        converter.TryFormat(combination, buffer, out _);
        converter.TryFormat(undefined, buffer, out _);

        // Act: 10,000 calls
        // Smallest of several windows: one noisy window (allocation-context refill, parallel GC) cannot fail it.
        long allocated = AllocationProbe.MinAllocated(() =>
        {
            for (int i = 0; i < 5_000; i++)
            {
                converter.TryFormat(combination, buffer, out _);
                converter.TryFormat(undefined, buffer, out _);
            }
        });

        // Assert
        await Assert.That(allocated).IsLessThan(1024);
    }
}
