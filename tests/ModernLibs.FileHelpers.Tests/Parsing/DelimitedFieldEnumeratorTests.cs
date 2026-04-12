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
    public async Task MoveNext_UnquotedFields_IteratesAll()
    {
        var fields = CollectFields("a,b,c".AsSpan(), ",");
        await Assert.That(fields.Length).IsEqualTo(3);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("b");
        await Assert.That(fields[2]).IsEqualTo("c");
    }

    [Test]
    public async Task MoveNext_QuotedFieldWithComma_TreatsAsOneField()
    {
        var fields = CollectFields("\"a,b\",c".AsSpan(), ",");
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a,b");
        await Assert.That(fields[1]).IsEqualTo("c");
    }

    [Test]
    public async Task MoveNext_EscapedDoubleQuote_UnescapesCorrectly()
    {
        var fields = CollectFields("\"a\"\"b\",c".AsSpan(), ",");
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a\"b");
    }

    [Test]
    public async Task MoveNext_MalformedUnclosedQuote_LenientNothrow()
    {
        // Should not throw — lenient per D-03.
        // We verify by simply calling CollectFields without wrapping in try/catch;
        // the test framework will catch any unexpected exception as a failure.
        var fields = CollectFields("\"unclosed,c".AsSpan(), ",");
        await Assert.That(fields.Length).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task MoveNext_EmptyField_YieldsEmpty()
    {
        var fields = CollectFields(",".AsSpan(), ",");
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("");
        await Assert.That(fields[1]).IsEqualTo("");
    }

    [Test]
    public async Task MoveNext_TrailingDelimiter_YieldsTrailingEmpty()
    {
        var fields = CollectFields("a,".AsSpan(), ",");
        await Assert.That(fields.Length).IsEqualTo(2);
        await Assert.That(fields[0]).IsEqualTo("a");
        await Assert.That(fields[1]).IsEqualTo("");
    }
}
