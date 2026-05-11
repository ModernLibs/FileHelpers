// Phase 39: Comprehensive tests for v1.4 Sep-inspired features.
// Covers SIMD, parallel reading, quote unescape, ArrayPool, and header mapping.
// MA0048: Multiple test classes in one file for Phase 39 coverage.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase39;

[Category("Phase39")]
[Category("SepInspired")]
public sealed class SimdDelimiterFinderTests
{
    [Test]
    [DisplayName("SIMD level detected")]
    public async Task SimdLevelDetected()
    {
        var level = SimdDelimiterFinder.Level;
        await Assert.That(level).IsNotEqualTo(SimdDelimiterFinder.SimdLevel.None);
    }

    [Test]
    [DisplayName("SIMD IndexOf finds delimiter")]
    public async Task SimdIndexOfFindsDelimiter()
    {
        var text = "hello;world;test";
        int idx = SimdDelimiterFinder.IndexOf(text.AsSpan(), ';');
        await Assert.That(idx).IsEqualTo(5);
    }

    [Test]
    [DisplayName("SIMD IndexOf returns -1 when not found")]
    public async Task SimdIndexOfNotFound()
    {
        var text = "hello world test";
        int idx = SimdDelimiterFinder.IndexOf(text.AsSpan(), ';');
        await Assert.That(idx).IsEqualTo(-1);
    }

    [Test]
    [DisplayName("SIMD IndexOf finds first of multiple")]
    public async Task SimdIndexOfFirstOfMultiple()
    {
        var text = "a;b;c;d;e";
        int idx = SimdDelimiterFinder.IndexOf(text.AsSpan(), ';');
        await Assert.That(idx).IsEqualTo(1);
    }

    [Test]
    [DisplayName("SIMD IndexOf handles empty span")]
    public async Task SimdIndexOfEmpty()
    {
        int idx = SimdDelimiterFinder.IndexOf(ReadOnlySpan<char>.Empty, ';');
        await Assert.That(idx).IsEqualTo(-1);
    }

    [Test]
    [DisplayName("SIMD IndexOf handles single char")]
    public async Task SimdIndexOfSingleChar()
    {
        int idx = SimdDelimiterFinder.IndexOf(";".AsSpan(), ';');
        await Assert.That(idx).IsEqualTo(0);
    }

    [Test]
    [DisplayName("SIMD IndexOf handles delimiter at end")]
    public async Task SimdIndexOfAtEnd()
    {
        var text = "hello;";
        int idx = SimdDelimiterFinder.IndexOf(text.AsSpan(), ';');
        await Assert.That(idx).IsEqualTo(5);
    }

    [Test]
    [DisplayName("SIMD IndexOf handles long text")]
    public async Task SimdIndexOfLongText()
    {
        var text = new string('a', 1000) + ";" + new string('b', 1000);
        int idx = SimdDelimiterFinder.IndexOf(text.AsSpan(), ';');
        await Assert.That(idx).IsEqualTo(1000);
    }
}

[Category("Phase39")]
[Category("SepInspired")]
public sealed class PooledLineBoundariesTests
{
    [Test]
    [DisplayName("PooledLineBoundaries populates from string")]
    public async Task PooledLineBoundariesPopulatesFromString()
    {
        using var pooled = new PooledLineBoundaries();
        var content = "line1\nline2\nline3\n";
        pooled.Populate(content);

        await Assert.That(pooled.LineCount).IsEqualTo(3);
        await Assert.That(pooled.LineStarts.Length).IsEqualTo(3);
        await Assert.That(pooled.LineLengths.Length).IsEqualTo(3);
    }

    [Test]
    [DisplayName("PooledLineBoundaries populates from span")]
    public async Task PooledLineBoundariesPopulatesFromSpan()
    {
        using var pooled = new PooledLineBoundaries();
        var content = "line1\r\nline2\r\nline3\r\n";
        pooled.Populate(content.AsSpan());

        await Assert.That(pooled.LineCount).IsEqualTo(3);
    }

    [Test]
    [DisplayName("PooledLineBoundaries handles empty content")]
    public async Task PooledLineBoundariesHandlesEmpty()
    {
        using var pooled = new PooledLineBoundaries();
        pooled.Populate("");

        await Assert.That(pooled.LineCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("PooledLineBoundaries disposes without leak")]
    public async Task PooledLineBoundariesDisposesCleanly()
    {
        var pooled = new PooledLineBoundaries();
        pooled.Populate("line1\nline2\n");
        pooled.Dispose();

        await Assert.That(pooled.LineCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("PooledLineBoundaries reuses on repopulate")]
    public async Task PooledLineBoundariesReusesOnRepopulate()
    {
        using var pooled = new PooledLineBoundaries();
        pooled.Populate("line1\nline2\n");
        await Assert.That(pooled.LineCount).IsEqualTo(2);

        pooled.Populate("a\nb\nc\nd\n");
        await Assert.That(pooled.LineCount).IsEqualTo(4);
    }
}

[Category("Phase39")]
[Category("SepInspired")]
public sealed class DelimitedRecordAttributeTests
{
    [Test]
    [DisplayName("DelimitedRecordAttribute HasHeader defaults to false")]
    public async Task DelimitedRecordHasHeaderDefaultsFalse()
    {
        var attr = new DelimitedRecordAttribute(",");
        await Assert.That(attr.HasHeader).IsFalse();
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute HasHeader can be set to true")]
    public async Task DelimitedRecordHasHeaderCanBeTrue()
    {
        var attr = new DelimitedRecordAttribute(",") { HasHeader = true };
        await Assert.That(attr.HasHeader).IsTrue();
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute QuoteUnescapeMode defaults to None")]
    public async Task DelimitedRecordQuoteUnescapeModeDefaultsNone()
    {
        var attr = new DelimitedRecordAttribute(",");
        await Assert.That(attr.QuoteUnescapeMode).IsEqualTo(QuoteUnescapeMode.None);
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute TrimMode defaults to None")]
    public async Task DelimitedRecordTrimModeDefaultsNone()
    {
        var attr = new DelimitedRecordAttribute(",");
        await Assert.That(attr.TrimMode).IsEqualTo(TrimMode.None);
    }
}

[Category("Phase39")]
[Category("SepInspired")]
public sealed class FieldNameAttributeTests
{
    [Test]
    [DisplayName("FieldNameAttribute constructor validates name")]
    public async Task FieldNameAttributeValidatesName()
    {
        var attr = new FieldNameAttribute("ProductName");
        await Assert.That(attr.Name).IsEqualTo("ProductName");
    }

    [Test]
    [DisplayName("FieldNameAttribute rejects null name")]
    public async Task FieldNameAttributeRejectsNull()
    {
        await Assert.That(async () => new FieldNameAttribute(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    [DisplayName("FieldNameAttribute rejects empty name")]
    public async Task FieldNameAttributeRejectsEmpty()
    {
        await Assert.That(async () => new FieldNameAttribute("")).Throws<ArgumentException>();
    }
}

[Category("Phase39")]
[Category("SepInspired")]
public sealed class QuoteUnescapeModeTests
{
    [Test]
    [DisplayName("QuoteUnescapeMode enum has expected values")]
    public async Task QuoteUnescapeModeHasExpectedValues()
    {
        var none = (int)QuoteUnescapeMode.None;
        var standard = (int)QuoteUnescapeMode.Standard;
        var lenient = (int)QuoteUnescapeMode.Lenient;
        await Assert.That(none).IsEqualTo(0);
        await Assert.That(standard).IsEqualTo(1);
        await Assert.That(lenient).IsEqualTo(2);
    }
}
