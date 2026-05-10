#pragma warning disable CA1707
#pragma warning disable MA0004

using Xunit;

namespace ModernLibs.FileHelpers.Mutation.Tests;

/// <summary>
/// xUnit mutation-target tests for <see cref="DelimitedFieldEnumerator"/>.
/// Covers all parse paths: delimiter detection, quoted fields, escape sequences,
/// per-field delimiters, and lenient edge-case behavior (MUT-01).
/// </summary>
public sealed class DelimitedFieldEnumeratorMutationTests
{
    [Fact]
    public void MoveNext_SingleCharDelimiter_ParsesFields()
    {
        // Arrange: comma-delimited line
        var line = "a,b,c".AsSpan();
        var enumerator = new DelimitedFieldEnumerator(line, ",");

        // Act & Assert: iterate all fields
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_MultiCharDelimiter_ParsesFields()
    {
        // Arrange: multi-char delimiter
        var line = "a|||b|||c".AsSpan();
        var enumerator = new DelimitedFieldEnumerator(line, "|||");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_EmptyLine_YieldsSingleEmptyField()
    {
        // Arrange: empty input
        var enumerator = new DelimitedFieldEnumerator("".AsSpan(), ",");

        // Act: first MoveNext on empty span yields one empty field
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        // Act: subsequent MoveNext returns false (termination guard)
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_TrailingDelimiter_YieldsTrailingEmptyField()
    {
        // Arrange: trailing delimiter
        var enumerator = new DelimitedFieldEnumerator("a,b,".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_NoTrailingDelimiter_LastField()
    {
        // Arrange: no trailing delimiter
        var enumerator = new DelimitedFieldEnumerator("a,b".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_NoEscapes_ReturnsContent()
    {
        // Arrange: simple quoted field
        var enumerator = new DelimitedFieldEnumerator("\"hello\"".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("hello", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_WithEscapes_Unescapes()
    {
        // Arrange: quoted field with one escape
        var enumerator = new DelimitedFieldEnumerator("\"he\"\"llo\"".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("he\"llo", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_MultipleEscapes_UnescapesAll()
    {
        // Arrange: multiple escape sequences
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"\"\"b\"".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"\"b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_UnclosedQuote_LenientReturnsRest()
    {
        // Arrange: unclosed quote (lenient mode)
        var enumerator = new DelimitedFieldEnumerator("\"hello".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("hello", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_ContentAfterQuote_DelimiterPresent()
    {
        // Arrange: quoted field followed by delimiter and another field
        var enumerator = new DelimitedFieldEnumerator("\"a\",b".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_ContentAfterQuote_NoDelimiter_Lenient()
    {
        // Arrange: content after closing quote with no delimiter (lenient)
        var enumerator = new DelimitedFieldEnumerator("\"a\"b".AsSpan(), ",");

        // Act: yields "a", sets _atEnd because no delimiter found after quote
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_ContentAfterQuote_WithExtraContent_Lenient()
    {
        // Arrange: extra content between closing quote and delimiter (lenient)
        var enumerator = new DelimitedFieldEnumerator("\"a\"xyz,b".AsSpan(), ",");

        // Act: yields "a", skips past delimiter to "b"
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_UsesOverride()
    {
        // Arrange: per-field delimiter overrides
        var line = "a;b,c".AsSpan();
        var fieldDelimiters = new string?[] { ";", "," };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_NullEntry_UsesDefault()
    {
        // Arrange: null entry in per-field array falls back to default
        var line = "a,b".AsSpan();
        var fieldDelimiters = new string?[] { null, null };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_EmptyEntry_UsesDefault()
    {
        // Arrange: empty string entry in per-field array falls back to default
        var line = "a,b".AsSpan();
        var fieldDelimiters = new string?[] { "", "," };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_EscapeLoop_NoClosingQuote_LenientCopiesRest()
    {
        // Arrange: escape sequence followed by unclosed quote
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_EscapeLoop_FoundClose_DelimiterPresent()
    {
        // Arrange: escape sequence followed by closing quote and delimiter
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\",c".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_EscapeLoop_FoundClose_NoDelimiter_SetsAtEnd()
    {
        // Arrange: escape sequence followed by closing quote but no delimiter
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\"c".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_EscapeLoop_FoundClose_ExtraContent_Lenient()
    {
        // Arrange: escape + closing quote + extra content before delimiter (lenient)
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\"xyz,c".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ConsecutiveQuotedFields_ParsesAll()
    {
        // Arrange: consecutive quoted fields
        var enumerator = new DelimitedFieldEnumerator("\"a\",\"b\",\"c\"".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_EmptyQuotedField_ReturnsEmpty()
    {
        // Arrange: empty quoted field
        var enumerator = new DelimitedFieldEnumerator("\"\"".AsSpan(), ",");

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_AfterEnd_ReturnsFalse()
    {
        // Arrange: exhaust the enumerator
        var enumerator = new DelimitedFieldEnumerator("a".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());

        // Act: _atEnd is set, subsequent calls return false
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void GetEnumerator_ReturnsSelf_ForForeachSupport()
    {
        // Arrange
        var enumerator = new DelimitedFieldEnumerator("a,b".AsSpan(), ",");

        // Act
        var returned = enumerator.GetEnumerator();

        // Assert
        Assert.Equal(enumerator.Current, returned.Current);
    }

    [Fact]
    public void HasUnclosedQuote_DetectsUnbalancedQuotes()
    {
        // Arrange & Act & Assert
        Assert.False(DelimitedFieldEnumerator.HasUnclosedQuote("hello"));
        Assert.False(DelimitedFieldEnumerator.HasUnclosedQuote("\"hello\""));
        Assert.True(DelimitedFieldEnumerator.HasUnclosedQuote("\"hello"));
        Assert.False(DelimitedFieldEnumerator.HasUnclosedQuote("\"\""));
    }
}
