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
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Descriptors;

/// <summary>The hidden read helpers of <see cref="RecordParsing"/> equal the code 4.9 emits.</summary>
[Category("Descriptors")]
public sealed class RecordParsingHelperTests
{
    [Test]
    public async Task CreateString_Empty_Is_String_Empty()
    {
        await Assert.That(ReferenceEquals(RecordParsing.CreateString(default), string.Empty)).IsTrue();
        await Assert.That(ReferenceEquals(RecordParsing.CreateString("abc".AsSpan(1, 0)), string.Empty)).IsTrue();
    }

    [Test]
    public async Task CreateString_One_Char_Below_128_Is_Cached()
    {
        await Assert.That(ReferenceEquals(RecordParsing.CreateString("a"), RecordParsing.CreateString("xa".AsSpan(1)))).IsTrue();
        await Assert.That(ReferenceEquals(RecordParsing.CreateString("\u007F"), RecordParsing.CreateString("\u007F"))).IsTrue();
        await Assert.That(RecordParsing.CreateString("a")).IsEqualTo("a");
    }

    [Test]
    public async Task CreateString_One_Char_From_128_Is_Not_Cached()
    {
        string first = RecordParsing.CreateString("\u0080");
        string second = RecordParsing.CreateString("\u0080");
        await Assert.That(first).IsEqualTo(second);
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task CreateString_Longer_Values_Are_Copied()
    {
        string source = "hello";
        string result = RecordParsing.CreateString(source);
        await Assert.That(result).IsEqualTo("hello");
        await Assert.That(ReferenceEquals(result, source)).IsFalse();
    }

    [Test]
    public async Task Unquote_Without_A_Doubled_Quote_Returns_The_Same_Span()
    {
        (bool overlaps, int offset, int length, int expectedLength) = OverlapOfNoPair();
        await Assert.That(overlaps).IsTrue();
        await Assert.That(offset).IsEqualTo(0);
        await Assert.That(length).IsEqualTo(expectedLength);
    }

    private static (bool Overlaps, int Offset, int Length, int ExpectedLength) OverlapOfNoPair()
    {
        ReadOnlySpan<char> inner = "a\"b,c".AsSpan();
        var result = RecordParsing.Unquote(inner, '"');
        bool overlaps = inner.Overlaps(result, out int offset);
        return (overlaps, offset, result.Length, inner.Length);
    }

    [Test]
    [Arguments("a\"\"b", "a\"b")]
    [Arguments("\"\"", "\"")]
    [Arguments("a\"", "a\"")]
    [Arguments("x\r\n\"\"y", "x\r\n\"y")]
    [Arguments("é\"\"ł", "é\"ł")]
    public async Task Unquote_Fixed_Cases(string inner, string expected)
    {
        await Assert.That(RecordParsing.Unquote(inner, '"').ToString()).IsEqualTo(expected);
        await Assert.That(RecordParsing.UnquoteToString(inner, '"')).IsEqualTo(expected);
        await Assert.That(LegacyEmittedCopies.Strip(inner, '"')).IsEqualTo(expected);
    }

    [Test]
    public async Task Unquote_Equals_The_4_9_Loop_On_Fuzzed_Input()
    {
        char[] alphabet = ['"', '\'', 'a', ',', '\r', '\n', 'é'];
        char[] quotes = ['"', '\''];
        var random = new Random(4916);
        var buffer = new char[40];
        int mismatches = 0;
        string? first = null;
        for (int n = 0; n < 200_000; n++)
        {
            int length = random.Next(0, 41);
            for (int i = 0; i < length; i++)
                buffer[i] = alphabet[random.Next(alphabet.Length)];
            char quote = quotes[random.Next(quotes.Length)];
            var inner = buffer.AsSpan(0, length);
            string expected = LegacyEmittedCopies.Strip(inner, quote);
            string span = RecordParsing.Unquote(inner, quote).ToString();
            string text = RecordParsing.UnquoteToString(inner, quote);
            if (!string.Equals(span, expected, StringComparison.Ordinal) || !string.Equals(text, expected, StringComparison.Ordinal))
            {
                mismatches++;
                first ??= $"quote {quote} input [{new string(inner)}] expected [{expected}] span [{span}] string [{text}]";
            }
        }
        await Assert.That(mismatches).IsEqualTo(0).Because(first ?? "");
    }

    [Test]
    public async Task Error_Helpers_Equal_The_Emitted_Ones()
    {
        var inner = new InvalidOperationException("inner");
        for (int k = 0; k < 5; k++)
        {
            foreach (var (what, actual, expected) in CompareErrorHelpers(k, inner))
                await Assert.That(actual).IsEqualTo(expected).Because($"case {k}: {what}");
        }
    }

    private static List<(string What, object? Actual, object? Expected)> CompareErrorHelpers(int k, Exception inner)
    {
        // Spans cannot live across an await, so every comparison is collected here.
        string record = "abc,defg,hi";
        string copy = "copy";
        ReadOnlySpan<char> value = k switch
        {
            0 => record.AsSpan(3, 2),
            1 => record.AsSpan(record.Length, 0),
            2 => record.AsSpan(4, 0),
            3 => default,
            _ => copy.AsSpan(),
        };
        var list = new List<(string, object?, object?)>
        {
            ("Column", RecordParsing.Column(record, value), LegacyEmittedCopies.Column(record, value)),
            ("Column absolute", RecordParsing.Column(record, value), k switch { 0 => 4, 1 => 12, 2 => 5, _ => -1 }),
        };

        var expected = LegacyEmittedCopies.ConvertError(record, value, typeof(int), "Id", "extra", inner);
        var actual = RecordParsing.ConvertError(record, value, typeof(int), "Id", "extra", inner);
        list.Add(("type", actual.GetType(), expected.GetType()));
        list.Add(("Message", actual.Message, expected.Message));
        list.Add(("LineNumber", actual.LineNumber, -1));
        list.Add(("ColumnNumber", actual.ColumnNumber, expected.ColumnNumber));
        list.Add(("FieldName", actual.FieldName, expected.FieldName));
        list.Add(("FieldStringValue", actual.FieldStringValue, expected.FieldStringValue));
        list.Add(("MessageExtra", actual.MessageExtra, expected.MessageExtra));
        list.Add(("InnerException", ReferenceEquals(actual.InnerException, inner), true));

        var noExtra = RecordParsing.ConvertError(record, value, typeof(int), "Id", null, null);
        var noExtraExpected = LegacyEmittedCopies.ConvertError(record, value, typeof(int), "Id", null, null);
        list.Add(("Message without extra", noExtra.Message, noExtraExpected.Message));
        list.Add(("no inner", noExtra.InnerException, null));

        var nv = RecordParsing.NullValueNotFound(record, value, "Amount", "Order");
        var nvExpected = LegacyEmittedCopies.NullValueNotFound(record, value, "Amount", "Order");
        list.Add(("NullValueNotFound Message", nv.Message, nvExpected.Message));
        list.Add(("NullValueNotFound LineNumber", nv.LineNumber, nvExpected.LineNumber));
        list.Add(("NullValueNotFound ColumnNumber", nv.ColumnNumber, nvExpected.ColumnNumber));
        return list;
    }

    [Test]
    public async Task NullValueNotFound_Has_The_Emitted_Text()
    {
        // A value outside the record has column -1 and line -1, so Message has no "Column: C." prefix and is the text itself.
        var ex = RecordParsing.NullValueNotFound("a,b", default, "Amount", "Order");
        await Assert.That(ex.Message).IsEqualTo("No value found for the value type field: 'Amount' Class: 'Order'. You must use the [FieldNullValue] attribute because this is a value type and can't be null or use a Nullable Type instead of the current type.");
        await Assert.That(ex.ColumnNumber).IsEqualTo(-1);

        // Inside the record, the position prefix of FileHelpersException is added.
        var inside = RecordParsing.NullValueNotFound("a,b", "a,b".AsSpan(2), "Amount", "Order");
        await Assert.That(inside.ColumnNumber).IsEqualTo(3);
        await Assert.That(inside.Message).StartsWith("Column: 3. No value found");
    }

    [Test]
    public async Task ConverterFailure_Keeps_Or_Builds_The_Extra()
    {
        string record = "a,b";
        var convert = new ConvertException("b", typeof(int), "Id", -1, -1, "kept extra", null);
        var kept = RecordParsing.ConverterFailure(record, record.AsSpan(2), typeof(int), "Id", "MyConv", convert);
        await Assert.That(kept.MessageExtra).IsEqualTo("kept extra");
        await Assert.That(kept.InnerException).IsSameReferenceAs(convert);

        var boom = new InvalidOperationException("boom");
        var built = RecordParsing.ConverterFailure(record, record.AsSpan(2), typeof(int), "Id", "MyConv", boom);
        await Assert.That(built.MessageExtra).IsEqualTo("Your custom converter: MyConv throws an InvalidOperationException with the message: boom");
        await Assert.That(built.InnerException).IsSameReferenceAs(boom);
        var legacy = LegacyEmittedCopies.ConverterFailure(record, record.AsSpan(2), typeof(int), "Id", "MyConv", boom);
        await Assert.That(built.Message).IsEqualTo(legacy.Message);
    }

    [Test]
    public async Task ConverterFailure_With_A_Null_Exception_Throws_ArgumentNullException()
    {
        await Assert.That(() => RecordParsing.ConverterFailure("a,b", "a,b".AsSpan(2), typeof(int), "Id", "MyConv", null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task StringConverter_Uses_The_Cache()
    {
        bool a = StringConverter.Instance.TryParse("x", out string first);
        bool b = StringConverter.Instance.TryParse("x", out string second);
        await Assert.That(a && b).IsTrue();
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }
}
