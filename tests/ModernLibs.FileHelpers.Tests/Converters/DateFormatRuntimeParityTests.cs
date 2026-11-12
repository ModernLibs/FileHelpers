// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>T01: invalid custom date formats are rejected the same way on every runtime.</summary>
[Category("Converters")]
public sealed class DateFormatRuntimeParityTests
{
    [Test]
    [Arguments("%")]           // lone % is an invalid custom format
    [Arguments("yyyy-MM-dd'")] // unterminated literal
    public async Task DateOnly_And_TimeOnly_Converters_RejectAnInvalidFormat_OnEveryRuntime(string format)
    {
        // The constructors validate the format (3.x messages) and throw BadUsageException; they never get as
        // far as TryParse. The test pins that this holds on every target.
        await Assert.That(() => new DateOnlyConverter(format)).Throws<BadUsageException>();
        await Assert.That(() => new TimeOnlyConverter(format)).Throws<BadUsageException>();
    }

    // Formats that pass DateFormat.Validate but are wrong for the type: TryParse must return false, not throw
    // (.NET 11 changed what DateOnly/TimeOnly.TryParseExact do with such formats; design doc risk 9).
    [Test]
    [Arguments("HH:mm")]
    [Arguments("t")]
    public async Task DateOnlyConverter_TryParse_ReturnsFalse_ForATimeOnlyFormat(string format)
    {
        await Assert.That(new DateOnlyConverter(format).TryParse("2026-11-11", out _)).IsFalse();
    }

    [Test]
    [Arguments("yyyy-MM-dd")]
    [Arguments("d")]
    public async Task TimeOnlyConverter_TryParse_ReturnsFalse_ForADateOnlyFormat(string format)
    {
        await Assert.That(new TimeOnlyConverter(format).TryParse("10:00:00", out _)).IsFalse();
    }
}
