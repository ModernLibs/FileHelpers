// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// TDD RED tests for DelimitedFieldEnumerator per-field delimiter support (FDEL-04).
/// These tests define the expected behavior for mixed-delimiter parsing.
/// </summary>
[Category("Phase17")]
[Category("Parsing")]
public sealed class DelimitedFieldEnumeratorPerFieldTests
{
    // CA1861: static readonly to avoid re-creating arrays across test calls.
    private static readonly string?[] s_semicolonComma = [";", ","];
    private static readonly string?[] s_semicolonNullPipe = [";", null, "|"];
    private static readonly string?[] s_allNull = [null, null, null];

    private static string[] CollectFieldsWithPerFieldDelimiters(ReadOnlySpan<char> line, string defaultDelimiter, ReadOnlySpan<string?> fieldDelimiters)
    {
        var results = new System.Collections.Generic.List<string>();
        var enumerator = new DelimitedFieldEnumerator(line, defaultDelimiter.AsSpan(), fieldDelimiters);
        foreach (var field in enumerator)
            results.Add(field.ToString());
        return results.ToArray();
    }

    /// <summary>
    /// FDEL-04: Enumerator can accept per-field delimiter array in constructor.
    /// </summary>
    [Test]
    [DisplayName("DelimitedFieldEnumerator accepts per-field delimiter array in constructor")]
    public async Task ConstructorWithPerFieldDelimiters_AcceptsDelimiterArray()
    {
        // Arrange: Create line with mixed delimiters "a;b,c"
        // Act: Parse with per-field delimiters [";", ","]
        var fields = CollectFieldsWithPerFieldDelimiters("a;b,c".AsSpan(), ",", s_semicolonComma.AsSpan());

        // Assert: Three fields extracted
        await Assert.That(fields.Length).IsEqualTo(3);
    }

    /// <summary>
    /// FDEL-04: First field uses first delimiter, second field uses second delimiter.
    /// </summary>
    [Test]
    [DisplayName("MoveNext uses correct delimiter per field position")]
    public async Task MoveNext_UsesCorrectDelimiterPerFieldPosition()
    {
        // Arrange: Line "a;b,c" where first delimiter is ";" and second is ","
        // Act: Parse with per-field delimiters
        var fields = CollectFieldsWithPerFieldDelimiters("a;b,c".AsSpan(), ",", s_semicolonComma.AsSpan());

        // Assert: Three fields with correct values
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }

    /// <summary>
    /// FDEL-04: Per-field delimiter overrides default constructor delimiter.
    /// </summary>
    [Test]
    [DisplayName("Per-field delimiter overrides default constructor delimiter")]
    public async Task PerFieldDelimiter_OverridesConstructorDelimiter()
    {
        // Arrange: Line "a;b" with default delimiter "," but per-field override ";"
        // Act: Parse with per-field delimiter [";"] - should split on semicolon
        string?[] perFieldDelims = new string?[] { ";", null };
        var fields = CollectFieldsWithPerFieldDelimiters("a;b".AsSpan(), ",", perFieldDelims.AsSpan());

        // Assert: Two fields split by semicolon (not comma)
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
    }

    /// <summary>
    /// FDEL-04: Null in delimiter array falls back to default delimiter.
    /// </summary>
    [Test]
    [DisplayName("Null entry in field delimiter array falls back to default delimiter")]
    public async Task NullDelimiter_FallsBackToDefault()
    {
        // Arrange: Line "a;b|c" with delimiters [";", null, "|"]
        // null means use default (",")
        // Act: Parse with mixed delimiters including null
        var fields = CollectFieldsWithPerFieldDelimiters("a;b,c".AsSpan(), ",", s_semicolonNullPipe.AsSpan());

        // Assert: Three fields where second uses default "," and third uses "|"
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }

    /// <summary>
    /// FDEL-04: Mixed delimiters "a;b,c" with delimiters [";", ","] yields "a", "b", "c".
    /// </summary>
    [Test]
    [DisplayName("Mixed delimiters parse correctly - semicolon then comma")]
    public async Task MixedDelimiters_ParsesCorrectly()
    {
        // Arrange: Line "a;b,c" with delimiters [";", ","]
        // Act: Parse the line
        var fields = CollectFieldsWithPerFieldDelimiters("a;b,c".AsSpan(), ",", s_semicolonComma.AsSpan());

        // Assert: Three fields with correct values
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }

    /// <summary>
    /// FDEL-04: Quoted fields work correctly with per-field delimiter.
    /// </summary>
    [Test]
    [DisplayName("Quoted field with custom delimiter handles quotes correctly")]
    public async Task QuotedField_WithCustomDelimiter()
    {
        // Arrange: Line '"x;y";z' with delimiters [";", ","] (semicolon for first field)
        // The first field is quoted and contains semicolon
        // Act: Parse the quoted field
        string?[] perFieldDelims = new string?[] { ";", null };
        var fields = CollectFieldsWithPerFieldDelimiters("\"x;y\";z".AsSpan(), ",", perFieldDelims.AsSpan());

        // Assert: Two fields where first contains the embedded semicolon
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("x;y");
        await Assert.That(fields[1]).IsEqualTo("z");
    }

    /// <summary>
    /// FDEL-04: All null delimiters uses default for all fields.
    /// </summary>
    [Test]
    [DisplayName("All null delimiters uses default delimiter for all fields")]
    public async Task AllNullDelimiters_UsesDefault()
    {
        // Arrange: Line "a,b,c" with all null delimiters
        // Act: Parse with all null delimiters
        var fields = CollectFieldsWithPerFieldDelimiters("a,b,c".AsSpan(), ",", s_allNull.AsSpan());

        // Assert: Three fields split by comma (default)
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }

    /// <summary>
    /// FDEL-04: Empty delimiter array uses default for all fields.
    /// </summary>
    [Test]
    [DisplayName("Empty delimiter span uses default delimiter for all fields")]
    public async Task EmptyDelimiterSpan_UsesDefault()
    {
        // Arrange: Line "a,b,c" with empty delimiter array
        // Act: Parse with empty delimiters
        var fields = CollectFieldsWithPerFieldDelimiters("a,b,c".AsSpan(), ",", ReadOnlySpan<string?>.Empty);

        // Assert: Three fields split by comma (default)
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }
}
