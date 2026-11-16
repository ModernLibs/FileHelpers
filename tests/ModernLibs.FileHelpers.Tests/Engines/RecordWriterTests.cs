// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA5394: a seeded Random makes the fuzzed inputs repeatable; nothing here is security-relevant.
#pragma warning disable CA5394

using System.Buffers;
using System.Text;
using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The members of <see cref="RecordWriter"/> (T22, frozen by <c>t22-freeze-2026-11-16.md</c>), each against the
/// helper of 4.9.1 generated code it replaces (<see cref="LegacyEmittedWriteCopies"/>) on the same input. The writer
/// formats into a <see cref="RentedBufferWriter"/> after some committed text, so the record starts inside the buffer,
/// and the buffers start small, so the members grow it.
/// </summary>
[Category("Engines")]
public sealed class RecordWriterTests
{
    private const string NewLineMessage =
        "One value for the field Name has a new line inside. To allow this value to be written you must add a FieldQuoted attribute with the multiline option set.";

    /// <summary>A record "formatted" by a test: the action runs inside <see cref="IRecordWriterDescriptor{T}.Format(T, ref RecordWriter)"/>.</summary>
    internal delegate void WriterAction(ref RecordWriter writer);

    [Test]
    public async Task Default_Writer_Throws()
    {
        await Assert.That(() => { var w = default(RecordWriter); _ = w.Position; }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); _ = w.FreeSpan; }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Advance(0); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Grow(Int32Converter.Instance); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Reserve(0); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Write('a'); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Write("a".AsSpan()); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.WriteNewLine(); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Quote(0, '"', true, true, ",", "Name"); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => { var w = default(RecordWriter); w.Pad(0, 5, 0, ' ', false, "Name"); }).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Quote_Equals_The_4_9_Helper_On_Fuzzed_Text()
    {
        char[] alphabet = ['"', 'a', ',', ';', '\r', '\n', 'é'];
        string[] separators = [",", ";;"];
        var random = new Random(2210);
        var failures = new List<string>();
        int cases = 0;

        for (int i = 0; i < 100_000; i++)
        {
            var chars = new char[random.Next(41)];
            for (int c = 0; c < chars.Length; c++)
                chars[c] = alphabet[random.Next(alphabet.Length)];
            string text = new(chars);

            // Committed text before the record, text of the record before the field, and the first buffer size vary,
            // so the field starts at many offsets and Quote often grows the buffer.
            string committed = new('c', random.Next(20));
            string before = new('b', random.Next(4));
            int initialSize = random.Next(2) == 0 ? 1 : random.Next(1, 100);

            foreach (bool always in (bool[])[false, true])
            {
                foreach (bool multilineWrite in (bool[])[false, true])
                {
                    foreach (string separator in separators)
                    {
                        cases++;
                        string expected = Expect(committed, before + "|", () =>
                        {
                            var buffer = new ArrayBufferWriter<char>();
                            LegacyEmittedWriteCopies.WriteQuotedField(buffer, text, '"', always, multilineWrite, separator, "Name");
                            return buffer.WrittenSpan.ToString();
                        });
                        string actual = Run(committed, initialSize, (ref RecordWriter w) =>
                        {
                            w.Write(before.AsSpan());
                            int start = w.Position;
                            w.Write(text.AsSpan());
                            w.Quote(start, '"', always, multilineWrite, separator, "Name");
                            w.Write('|');
                        });
                        if (!string.Equals(expected, actual, StringComparison.Ordinal) && failures.Count < 5)
                            failures.Add($"text '{Show(text)}', always {always}, multilineWrite {multilineWrite}, separator '{separator}': expected '{Show(expected)}', got '{Show(actual)}'");
                    }
                }
            }
        }

        await Assert.That(cases).IsEqualTo(800_000);
        await Assert.That(string.Join("\n", failures)).IsEqualTo(string.Empty);

        // The error text, once more by name.
        string error = Run(string.Empty, 16, (ref RecordWriter w) =>
        {
            w.Write("a\nb".AsSpan());
            w.Quote(0, '"', false, false, ",", "Name");
        });
        await Assert.That(error).IsEqualTo("throw:BadUsageException:" + NewLineMessage + "|committed:");
    }

    [Test]
    public async Task Pad_Equals_The_4_9_Helper()
    {
        var failures = new List<string>();
        int cases = 0;
        foreach (int width in (int[])[1, 5, 10])
        {
            string[] texts = [string.Empty, new string('s', width - 1), new string('e', width), "x" + new string('l', width + 1)];
            foreach (string text in texts.Distinct(StringComparer.Ordinal))
            {
                foreach (int align in (int[])[0, 1, 2])
                {
                    foreach (char pad in (char[])[' ', '0'])
                    {
                        foreach (bool overflowError in (bool[])[false, true])
                        {
                            foreach (int initialSize in (int[])[1, 64])
                            {
                                cases++;
                                string expected = Expect("cc", "b|", () =>
                                {
                                    var buffer = new ArrayBufferWriter<char>();
                                    LegacyEmittedWriteCopies.WritePadded(buffer, text, width, align, pad, overflowError, "Amount");
                                    return buffer.WrittenSpan.ToString();
                                });
                                string actual = Run("cc", initialSize, (ref RecordWriter w) =>
                                {
                                    w.Write('b');
                                    int start = w.Position;
                                    w.Write(text.AsSpan());
                                    w.Pad(start, width, align, pad, overflowError, "Amount");
                                    w.Write('|');
                                });
                                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                                    failures.Add($"width {width}, text '{text}', align {align}, pad '{pad}', overflowError {overflowError}: expected '{expected}', got '{actual}'");
                            }
                        }
                    }
                }
            }
        }

        // 11 texts (width 1 has no shorter text but the empty one) x 3 aligns x 2 pads x 2 x 2 buffer sizes.
        await Assert.That(cases).IsEqualTo(11 * 3 * 2 * 2 * 2);
        await Assert.That(string.Join("\n", failures)).IsEqualTo(string.Empty);

        string overflow = Run(string.Empty, 16, (ref RecordWriter w) =>
        {
            w.Write("1234567".AsSpan());
            w.Pad(0, 5, 2, '0', true, "Amount");
        });
        await Assert.That(overflow).IsEqualTo("throw:InvalidOperationException:Field 'Amount' exceeds fixed length 5 (actual: 7 characters).|committed:");
    }

    [Test]
    [Arguments(5, "1234567", "12345")]
    [Arguments(6, "123456.78", "123456")]
    public async Task Pad_Truncates_Overflow_From_The_End(int width, string text, string expected)
    {
        var legacy = new ArrayBufferWriter<char>();
        LegacyEmittedWriteCopies.WritePadded(legacy, text, width, 2, ' ', false, "Amount");

        string actual = Run(string.Empty, 4, (ref RecordWriter w) =>
        {
            w.Write(text.AsSpan());
            w.Pad(0, width, 2, ' ', false, "Amount");
            w.Write('|');
        });

        await Assert.That(legacy.WrittenSpan.ToString()).IsEqualTo(expected);
        await Assert.That(actual).IsEqualTo(expected + "|");
    }

    [Test]
    [Arguments(0)]
    [Arguments(63)]
    [Arguments(64)]
    [Arguments(65)]
    [Arguments(5_000)]
    [Arguments(70_000)]
    [Arguments(0x100000)]
    public async Task Long_Converter_Output_And_A_100000_Char_String_Are_Written_Whole(int length)
    {
        var converter = new RepeatConverter();
        string longText = new('t', 100_000);

        string expected = Expect("committed", string.Empty, () =>
        {
            var buffer = new ArrayBufferWriter<char>();
            LegacyEmittedWriteCopies.WriteConverted(buffer, converter, length);
            buffer.Write(";".AsSpan());
            LegacyEmittedWriteCopies.WriteText(buffer, longText);
            return buffer.WrittenSpan.ToString();
        });
        string actual = Run("committed", 16, (ref RecordWriter w) =>
        {
            // The loop T23 emits for a converted field.
            int n;
            while (!converter.TryFormat(length, w.FreeSpan, out n))
                w.Grow(converter);
            w.Advance(n);
            w.Write(';');
            w.Write(longText.AsSpan());
        });

        await Assert.That(actual.Length).IsEqualTo("committed".Length + length + 1 + 100_000);
        await Assert.That(string.Equals(actual, expected, StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Converter_That_Never_Fits_Throws_The_Old_FormatException()
    {
        var legacyConverter = new NeverFitsConverter();
        var converter = new NeverFitsConverter();

        string expected = Expect("c", string.Empty, () =>
        {
            LegacyEmittedWriteCopies.WriteConverted(new ArrayBufferWriter<char>(), legacyConverter, 1);
            return string.Empty;
        });
        string actual = Run("c", 16, (ref RecordWriter w) =>
        {
            int n;
            while (!converter.TryFormat(1, w.FreeSpan, out n))
                w.Grow(converter);
            w.Advance(n);
        });

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(actual).EndsWith("could not be formatted by NeverFitsConverter.|committed:c");

        // Grow threw on the first free span of 0x100000 chars or more: every earlier one was smaller.
        int[] lengths = [.. converter.Lengths];
        await Assert.That(lengths[^1]).IsGreaterThanOrEqualTo(0x100000);
        await Assert.That(lengths[..^1].All(l => l < 0x100000)).IsTrue();
        for (int i = 1; i < lengths.Length; i++)
            await Assert.That(lengths[i]).IsGreaterThanOrEqualTo(Math.Max(2 * lengths[i - 1], 64)).Because($"growth step {i}");
    }

    [Test]
    public async Task WriteNewLine_Writes_The_Session_NewLine_And_Nothing_When_Empty()
    {
        static void Record(ref RecordWriter w)
        {
            w.Write('a');
            w.WriteNewLine();
            w.Write('b');
        }

        await Assert.That(Run("x", 1, Record, "|\n")).IsEqualTo("x" + "a|\nb");
        await Assert.That(Run("x", 1, Record, "\r\n")).IsEqualTo("x" + "a\r\nb");
        await Assert.That(Run("x", 1, Record, string.Empty)).IsEqualTo("x" + "ab");

        // A new buffer has "\r\n" until an engine sets it.
        using var buffer = new RentedBufferWriter(16);
        await Assert.That(buffer.NewLine).IsEqualTo("\r\n");
    }

    [Test]
    public async Task Position_FreeSpan_Advance_And_Grow_Keep_The_Record()
    {
        string text = Run("committed", 1, (ref RecordWriter w) =>
        {
            Check(w.Position == 0, "Position 0 at the start");
            w.Write("abc".AsSpan());
            int free = w.FreeSpan.Length;
            w.Grow(Int32Converter.Instance);
            Check(w.Position == 3, "Grow keeps Position");
            Check(w.FreeSpan.Length >= Math.Max(2 * free, 64), "Grow doubles the free space, at least 64");
            "de".AsSpan().CopyTo(w.FreeSpan);
            w.Advance(2);
            w.Reserve(10_000);
            Check(w.Position == 5 && w.FreeSpan.Length >= 10_000, "Reserve keeps Position and gives the space");
            w.Advance(0);
        });
        await Assert.That(text).IsEqualTo("committed" + "abcde");

        string tooFar = Run(string.Empty, 16, (ref RecordWriter w) => w.Advance(w.FreeSpan.Length + 1));
        await Assert.That(tooFar).StartsWith("throw:ArgumentOutOfRangeException:");
        string negative = Run(string.Empty, 16, (ref RecordWriter w) => w.Advance(-1));
        await Assert.That(negative).StartsWith("throw:ArgumentOutOfRangeException:");
    }

    [Test]
    public async Task A_Record_Whose_Format_Throws_Commits_Nothing()
    {
        string text = Run("committed", 1, (ref RecordWriter w) =>
        {
            w.Write(new string('x', 5000).AsSpan());
            throw new InvalidDataException("format failed");
        });

        await Assert.That(text).IsEqualTo("throw:InvalidDataException:format failed|committed:committed");
    }

    [Test]
    public async Task Edge_Arguments_Throw_The_Documented_Exceptions()
    {
        await Assert.That(Run(string.Empty, 16, (ref RecordWriter w) => w.Grow(null!)))
            .StartsWith("throw:ArgumentNullException:");
        await Assert.That(Run(string.Empty, 16, (ref RecordWriter w) => w.Reserve(-1)))
            .StartsWith("throw:ArgumentOutOfRangeException:");
        await Assert.That(Run(string.Empty, 16, (ref RecordWriter w) =>
        {
            w.Write("ab".AsSpan());
            w.Quote(3, '"', true, true, ",", "Name");
        })).StartsWith("throw:ArgumentOutOfRangeException:");
        await Assert.That(Run(string.Empty, 16, (ref RecordWriter w) =>
        {
            w.Write("ab".AsSpan());
            w.Quote(-1, '"', true, true, ",", "Name");
        })).StartsWith("throw:ArgumentOutOfRangeException:");
        await Assert.That(Run(string.Empty, 16, (ref RecordWriter w) =>
        {
            w.Write("ab".AsSpan());
            w.Pad(3, 5, 0, ' ', false, "Amount");
        })).StartsWith("throw:ArgumentOutOfRangeException:");
        await Assert.That(Run(string.Empty, 16, (ref RecordWriter w) =>
        {
            w.Write("ab".AsSpan());
            w.Pad(-1, 5, 0, ' ', false, "Amount");
        })).StartsWith("throw:ArgumentOutOfRangeException:");
    }

    [Test]
    [Arguments("")]
    [Arguments("ab")]
    public async Task Pad_Width_0_And_Quote_With_An_Empty_Separator_Equal_The_4_9_Helpers(string text)
    {
        foreach (bool overflowError in (bool[])[false, true])
        {
            string expected = Expect("c", "b|", () =>
            {
                var buffer = new ArrayBufferWriter<char>();
                LegacyEmittedWriteCopies.WritePadded(buffer, text, 0, 2, ' ', overflowError, "Amount");
                return buffer.WrittenSpan.ToString();
            });
            string actual = Run("c", 1, (ref RecordWriter w) =>
            {
                w.Write('b');
                int start = w.Position;
                w.Write(text.AsSpan());
                w.Pad(start, 0, 2, ' ', overflowError, "Amount");
                w.Write('|');
            });
            await Assert.That(actual).IsEqualTo(expected).Because($"Pad width 0, overflowError {overflowError}");
        }

        // An empty separator is found in every text (also in an empty one), so the value is always quoted.
        string quotedExpected = Expect("c", "b|", () =>
        {
            var buffer = new ArrayBufferWriter<char>();
            LegacyEmittedWriteCopies.WriteQuotedField(buffer, text, '"', false, false, string.Empty, "Name");
            return buffer.WrittenSpan.ToString();
        });
        string quoted = Run("c", 1, (ref RecordWriter w) =>
        {
            w.Write('b');
            int start = w.Position;
            w.Write(text.AsSpan());
            w.Quote(start, '"', false, false, string.Empty, "Name");
            w.Write('|');
        });
        await Assert.That(quoted).IsEqualTo(quotedExpected);
        await Assert.That(quoted).IsEqualTo("cb\"" + text + "\"|");
    }

    /// <summary>
    /// The growth size of <see cref="RentedBufferWriter"/>: twice the length, at least the required size, and capped
    /// at <see cref="Array.MaxLength"/> where the doubling of 4.9 overflowed <see cref="int"/>.
    /// </summary>
    [Test]
    [Arguments(16, 20, 32)]
    [Arguments(16, 100, 100)]
    [Arguments(0, 5, 5)]
    [Arguments(0x40000000, 0x40000001, 0x7FFFFFC7)]
    [Arguments(0x7FFFFFC7, 0x7FFFFFC7, 0x7FFFFFC7)]
    public async Task RentedBufferWriter_Growth_Doubles_And_Stops_At_Array_MaxLength(int length, int required, int expected)
    {
        await Assert.That(Array.MaxLength).IsEqualTo(0x7FFFFFC7);
        await Assert.That(RentedBufferWriter.NextSize(length, required)).IsEqualTo(expected);
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
            throw new InvalidOperationException("check failed: " + what);
    }

    /// <summary>
    /// Formats one record with <paramref name="action"/> into a <see cref="RentedBufferWriter"/> of
    /// <paramref name="initialSize"/> chars that already holds <paramref name="committed"/>. Returns the buffer text,
    /// or, when the action throws, <c>throw:&lt;type&gt;:&lt;message&gt;|committed:&lt;buffer text&gt;</c>.
    /// </summary>
    private static string Run(string committed, int initialSize, WriterAction action, string newLine = "\r\n")
    {
        using var buffer = new RentedBufferWriter(initialSize) { NewLine = newLine };
        buffer.Write(committed.AsSpan());
        try
        {
            buffer.Format(ActionDescriptor.Instance, action);
            return buffer.WrittenSpan.ToString();
        }
        catch (Exception ex)
        {
            return $"throw:{ex.GetType().Name}:{ex.Message}|committed:{buffer.WrittenSpan}";
        }
    }

    /// <summary>
    /// What <see cref="Run"/> gives when the record is <paramref name="before"/>, then what <paramref name="legacy"/>
    /// writes, then <paramref name="after"/>; when <paramref name="legacy"/> throws, the record leaves nothing.
    /// </summary>
    private static string Expect(string committed, string beforeAndAfter, Func<string> legacy)
    {
        int bar = beforeAndAfter.IndexOf('|', StringComparison.Ordinal);
        string before = bar < 0 ? beforeAndAfter : beforeAndAfter[..bar];
        string after = bar < 0 ? string.Empty : "|";
        try
        {
            return committed + before + legacy() + after;
        }
        catch (Exception ex)
        {
            return $"throw:{ex.GetType().Name}:{ex.Message}|committed:{committed}";
        }
    }

    private static string Show(string text) =>
        text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>A descriptor whose records are <see cref="WriterAction"/>s: <c>Format</c> runs the action.</summary>
    private sealed class ActionDescriptor : IRecordWriterDescriptor<WriterAction>
    {
        public static readonly ActionDescriptor Instance = new();

        public ReadOnlySpan<FieldDescriptor> Fields => default;

        public RecordOptions Options => default;

        public void Format(WriterAction record, ref RecordWriter writer) => record(ref writer);

        public void Format(WriterAction record, IBufferWriter<char> writer) => throw new NotSupportedException();

        public WriterAction Parse(ReadOnlySpan<char> lines, int lineCount) => throw new NotSupportedException();

        public WriterAction CreateDefaultInstance() => throw new NotSupportedException();
    }

    /// <summary>Formats the value <c>n</c> as <c>n</c> chars (<c>'0'</c> to <c>'9'</c> in turn); false when they do not fit.</summary>
    private sealed class RepeatConverter : ISpanConverter<int>
    {
        public bool TryParse(ReadOnlySpan<char> span, out int value)
        {
            value = span.Length;
            return true;
        }

        public bool TryFormat(int value, Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (destination.Length < value)
                return false;
            for (int i = 0; i < value; i++)
                destination[i] = (char)('0' + (i % 10));
            charsWritten = value;
            return true;
        }
    }

    /// <summary>Never fits; records the length of every destination it was given.</summary>
    private sealed class NeverFitsConverter : ISpanConverter<int>
    {
        public List<int> Lengths { get; } = [];

        public bool TryParse(ReadOnlySpan<char> span, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryFormat(int value, Span<char> destination, out int charsWritten)
        {
            Lengths.Add(destination.Length);
            charsWritten = 0;
            return false;
        }
    }
}
