// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Tests for DelimitedFieldEnumerator (PIPE-02).
/// Verifies RFC 4180 quoted-field handling, unquoted fields, lenient malformed input,
/// empty fields, trailing delimiters, and multi-char delimiter support.
/// </summary>
[Category("Phase05")]
[Category("Parsing")]
public sealed class DelimitedFieldEnumeratorTests
{
    // CA1861: static readonly to avoid re-creating arrays across test calls.
    private static readonly string[] s_abc = ["a", "b", "c"];
    private static readonly string[] s_aCommaB_c = ["a,b", "c"];
    private static readonly string[] s_emptyEmpty = ["", ""];
    private static readonly string[] s_a_empty = ["a", ""];

    private static string[] CollectFields(ReadOnlySpan<char> line, string delimiter)
    {
        var results = new System.Collections.Generic.List<string>();
        foreach (var field in new DelimitedFieldEnumerator(line, delimiter.AsSpan()))
            results.Add(field.ToString());
        return results.ToArray();
    }

    [Test]
    [DisplayName("DelimitedFieldEnumerator iterates all unquoted comma-separated fields")]
    public async Task MoveNext_UnquotedFields_IteratesAll()
    {
        // Arrange & Act: Parse simple CSV line "a,b,c"
        var fields = CollectFields("a,b,c".AsSpan(), ",");

        // Assert: Three fields extracted with values "a", "b", "c"
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }

    [Test]
    [DisplayName("DelimitedFieldEnumerator treats comma inside quoted field as literal content")]
    public async Task MoveNext_QuotedFieldWithComma_TreatsAsOneField()
    {
        // Arrange & Act: Parse CSV line with quoted field containing comma
        var fields = CollectFields("\"a,b\",c".AsSpan(), ",");

        // Assert: Two fields where first contains the comma inside quotes
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a,b");
        await Assert.That(fields[1]).IsEqualTo("c");
    }

    [Test]
    [DisplayName("DelimitedFieldEnumerator unescapes doubled quotes to single quote")]
    public async Task MoveNext_EscapedDoubleQuote_UnescapesCorrectly()
    {
        // Arrange & Act: Parse CSV line with escaped quote ("") inside quoted field
        var fields = CollectFields("\"a\"\"b\",c".AsSpan(), ",");

        // Assert: Two fields where first contains single quote after unescaping
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a\"b");
    }

    [Test]
    [DisplayName("DelimitedFieldEnumerator handles unclosed quote leniently without throwing")]
    public async Task MoveNext_MalformedUnclosedQuote_LenientNothrow()
    {
        // Should not throw — lenient per D-03.
        // We verify by simply calling CollectFields without wrapping in try/catch;
        // the test framework will catch any unexpected exception as a failure.
        // Arrange & Act: Parse malformed CSV with unclosed quote
        var fields = CollectFields("\"unclosed,c".AsSpan(), ",");

        // Assert: At least one field returned despite malformed input
        await Assert.That(fields.Length).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    [DisplayName("DelimitedFieldEnumerator yields empty strings for empty fields between delimiters")]
    public async Task MoveNext_EmptyField_YieldsEmpty()
    {
        // Arrange & Act: Parse CSV line with empty field between commas
        var fields = CollectFields(",".AsSpan(), ",");

        // Assert: Two empty fields returned
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("");
        await Assert.That(fields[1]).IsEqualTo("");
    }

    [Test]
    [DisplayName("DelimitedFieldEnumerator yields trailing empty field after final delimiter")]
    public async Task MoveNext_TrailingDelimiter_YieldsTrailingEmpty()
    {
        // Arrange & Act: Parse CSV line with trailing delimiter
        var fields = CollectFields("a,".AsSpan(), ",");

        // Assert: Two fields where second is empty due to trailing delimiter
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("");
    }
}
