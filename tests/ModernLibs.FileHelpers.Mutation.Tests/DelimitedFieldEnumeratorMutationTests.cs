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

    // --- Mutation-sensitive tests targeting surviving mutants ---

    [Fact]
    public void MoveNext_SingleCharDelimiter_DelimiterAtStartOfRemaining()
    {
        // Targets: afterClose.IndexOf(delimiter[0]) == 0 path (line 196-201)
        // Mutation: changing delimIdx == 0 to delimIdx != 0 would break this
        var enumerator = new DelimitedFieldEnumerator("\"a\",,b".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_NoDelimiterAfterClose_SetsAtEnd()
    {
        // Targets: delimIdx < 0 path after closing quote (line 203-206)
        // Mutation: changing delimIdx < 0 to delimIdx >= 0 would break this
        var enumerator = new DelimitedFieldEnumerator("\"a\"b".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.False(enumerator.MoveNext()); // atEnd should be true
    }

    [Fact]
    public void MoveNext_QuotedField_DelimiterNotAtStart_SkipsContent()
    {
        // Targets: delimIdx > 0 path (line 208-211) — lenient skip of content between close and delimiter
        // Mutation: changing delimIdx + delimiter.Length would produce wrong remaining
        var enumerator = new DelimitedFieldEnumerator("\"a\"XYZ,b".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_EscapePath_DelimiterAtStart()
    {
        // Targets: escape path delimIdx == 0 (line 273-275)
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\",c".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_EscapePath_NoDelimiterAfterClose()
    {
        // Targets: escape path delimIdx < 0 (line 277-280)
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\"c".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.False(enumerator.MoveNext()); // atEnd should be true
    }

    [Fact]
    public void MoveNext_QuotedField_EscapePath_DelimiterNotAtStart()
    {
        // Targets: escape path delimIdx > 0 (line 282-284)
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\"XYZ,c".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_EscapePath_NoCloseQuote_LenientCopiesRest()
    {
        // Targets: escape path q < 0 branch (line 234-241)
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_EscapePath_EmptyAfterClose_SetsAtEnd()
    {
        // Targets: escape path !remaining.IsEmpty == false (line 287-291)
        var enumerator = new DelimitedFieldEnumerator("\"a\"\"b\"".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a\"b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_IndexWithinBounds_UsesOverride()
    {
        // Targets: _fieldIndex < _fieldDelimiters.Length (line 74)
        var line = "a;b,c".AsSpan();
        var fieldDelimiters = new string?[] { ";", "," };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_IndexBeyondBounds_UsesDefault()
    {
        // Targets: _fieldIndex >= _fieldDelimiters.Length falls to default (line 82)
        var line = "a;b,c,d".AsSpan();
        var fieldDelimiters = new string?[] { ";" };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("d", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_NullEntry_FallsToDefault()
    {
        // Targets: _fieldDelimiters[_fieldIndex] is not null check (line 74)
        var line = "a,b".AsSpan();
        var fieldDelimiters = new string?[] { null };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_PerFieldDelimiter_EmptyStringEntry_FallsToDefault()
    {
        // Targets: !string.IsNullOrEmpty(del) check (line 77)
        var line = "a,b".AsSpan();
        var fieldDelimiters = new string?[] { "" };
        var enumerator = new DelimitedFieldEnumerator(line, ",", fieldDelimiters);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_NoQuoteStart_FastPath_DelimiterNotFound()
    {
        // Targets: span[0] != '"' fast path with delimIdx < 0 (line 123-128)
        var enumerator = new DelimitedFieldEnumerator("hello".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("hello", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_NoQuoteStart_FastPath_DelimiterFound()
    {
        // Targets: span[0] != '"' fast path with delimIdx >= 0 (line 129-133)
        var enumerator = new DelimitedFieldEnumerator("a,b".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_NoEscape_DelimiterAtStart()
    {
        // Targets: no-escape path delimIdx == 0 (line 196-201)
        var enumerator = new DelimitedFieldEnumerator("\"hello\",world".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("hello", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("world", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_NoEscape_DelimiterNotAtStart_Lenient()
    {
        // Targets: no-escape path delimIdx > 0 (line 208-211)
        var enumerator = new DelimitedFieldEnumerator("\"hello\"XYZ,world".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("hello", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("world", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void GetEnumerator_ForeachSupport_IteratesAllFields()
    {
        // Targets: GetEnumerator returns self, foreach uses MoveNext/Current
        var enumerator = new DelimitedFieldEnumerator("a,b,c".AsSpan(), ",");
        var fields = new System.Collections.Generic.List<string>();
        foreach (var field in enumerator)
            fields.Add(field.ToString());
        Assert.Equal(3, fields.Count);
        Assert.Equal("a", fields[0]);
        Assert.Equal("b", fields[1]);
        Assert.Equal("c", fields[2]);
    }

    [Fact]
    public void MoveNext_EmptyQuotedField_FollowedByDelimiter()
    {
        // Targets: empty quoted "" with delimiter at position 0
        var enumerator = new DelimitedFieldEnumerator("\"\",b".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedFieldWithComma_FollowedByEmptyField()
    {
        // Targets: quoted field with delimiter inside, followed by empty field
        var enumerator = new DelimitedFieldEnumerator("\"a,b\",,\"c\"".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a,b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_MultiCharDelimiter_FoundAtNonZeroPosition()
    {
        // Targets: multi-char delimiter scalar search path
        var enumerator = new DelimitedFieldEnumerator("a|||b|||c".AsSpan(), "|||");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_QuotedField_UnclosedQuoteWithDelimiter_LenientSetsAtEnd()
    {
        // Targets: unclosed quote path with no closing quote found (firstQuote < 0)
        var enumerator = new DelimitedFieldEnumerator("\"hello,world".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.Equal("hello,world", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_AfterEnd_MultipleCalls_ReturnFalse()
    {
        // Targets: _atEnd guard (line 102-103) — mutation of _atEnd would break this
        var enumerator = new DelimitedFieldEnumerator("a".AsSpan(), ",");
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }
}
