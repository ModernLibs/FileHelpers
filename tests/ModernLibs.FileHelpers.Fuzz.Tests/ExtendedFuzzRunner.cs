using System;
using System.Globalization;
using System.IO;
using System.Text;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Fuzz.Tests;

public static class ExtendedFuzzRunner
{
    private static readonly Random _random = new(42);
    private static int _totalTests;
    private static int _passed;
    private static int _failed;

    private static readonly Func<string>[] _generators =
    [
        GenerateRandomAscii,
        GenerateRandomUnicode,
        GenerateSurrogatePairs,
        GenerateZeroWidthChars,
        GenerateRtlMarkers,
        GenerateControlChars,
        GenerateNullBytes,
        GenerateMixedLineEndings,
        GenerateBomVariants,
        GenerateLongFields,
        GenerateEmptyAndWhitespace,
        GenerateQuotedEdgeCases,
        GenerateDelimiterChaos,
        GenerateNestedQuotes,
        GenerateEscapedChaos,
        GenerateNormalizationEdgeCases,
        GenerateTruncatedUtf8,
        GenerateRepeatingPatterns,
        GenerateMaxWidthFields,
        GenerateRandomBytesAsUtf8,
    ];

    public static int Run(int maxIterations = 10000)
    {
        Console.WriteLine("Extended Fuzz Runner - Generating harder inputs");
        Console.WriteLine($"Iterations: {maxIterations}");
        Console.WriteLine();

        for (int i = 0; i < maxIterations; i++)
        {
            var generator = _generators[_random.Next(_generators.Length)];
            var input = generator();
            _totalTests++;

            try
            {
                RunAllTargets(input);
                _passed++;
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"CRASH at iteration {_totalTests}:");
                Console.WriteLine($"  Generator: {generator.Method.Name}");
                Console.WriteLine($"  Input length: {input.Length}");
                Console.WriteLine($"  Input (first 200 chars): {Sanitize(input, 200)}");
                Console.WriteLine($"  Exception: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine();
            }

            if (_totalTests % 1000 == 0)
            {
                Console.WriteLine($"Progress: {_totalTests}/{maxIterations} ({_passed} passed, {_failed} failed)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Extended Fuzz Results ===");
        Console.WriteLine($"Total: {_totalTests}");
        Console.WriteLine($"Passed: {_passed}");
        Console.WriteLine($"Failed: {_failed}");

        return _failed;
    }

    private static void RunAllTargets(string input)
    {
        if (input.Length == 0) return;

        // SpanLineReader
        var lineReader = new SpanLineReader(input);
        int lineCount = 0;
        while (lineReader.TryReadLine(out var line))
        {
            lineCount++;
            if (line.IsEmpty) continue;

            // QuoteBalanceScanner
            int scanField = 0;
            _ = QuoteBalanceScanner.EndsInsideQuotes(line, startsInsideQuotes: false, ",", "\"\0\"", [" \t", null, " \t"], ref scanField);
            _ = QuoteBalanceScanner.CountFields(line, ",", "\"\"'", [null, " \t"]);

            // FixedLengthSlicer
            var slicer = new FixedLengthSlicer(line);
            int offset = _random.Next(Math.Max(0, line.Length - 10), line.Length + 5);
            int width = _random.Next(0, 20);
            var result = slicer.Slice(offset, width);
            _ = result.Length;

            if (lineCount > 1000) break;
        }

        // Converters
        var span = input.AsSpan().Trim();
        if (span.Length > 0)
        {
            _ = Int32Converter.Instance.TryParse(span, out _);
            _ = Int64Converter.Instance.TryParse(span, out _);
            _ = DoubleConverter.Instance.TryParse(span, out _);
            _ = DecimalConverter.Instance.TryParse(span, out _);
            _ = BoolConverter.Instance.TryParse(span, out _);
            _ = GuidConverter.Instance.TryParse(span, out _);
            _ = CharConverter.Instance.TryParse(span, out _);
            _ = StringConverter.Instance.TryParse(span, out _);
        }
    }

    private static string GenerateRandomAscii()
    {
        int length = _random.Next(1, 10000);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)_random.Next(32, 127);
        }
        return new string(chars);
    }

    private static string GenerateRandomUnicode()
    {
        int length = _random.Next(1, 5000);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            // Mix of ASCII, Latin, Greek, Cyrillic, CJK, Arabic, Hebrew
            int range = _random.Next(8);
            chars[i] = range switch
            {
                0 => (char)_random.Next(32, 127),           // ASCII
                1 => (char)_random.Next(0x00C0, 0x024F),    // Latin Extended
                2 => (char)_random.Next(0x0370, 0x03FF),    // Greek
                3 => (char)_random.Next(0x0400, 0x04FF),    // Cyrillic
                4 => (char)_random.Next(0x4E00, 0x9FFF),    // CJK
                5 => (char)_random.Next(0x0600, 0x06FF),    // Arabic
                6 => (char)_random.Next(0x0590, 0x05FF),    // Hebrew
                _ => (char)_random.Next(0x0E00, 0x0E7F),    // Thai
            };
        }
        return new string(chars);
    }

    private static string GenerateSurrogatePairs()
    {
        int length = _random.Next(1, 2000);
        var sb = new StringBuilder(length * 2);
        for (int i = 0; i < length; i++)
        {
        // Emoji and other supplementary plane characters
        int codepoint = _random.Next(0x1F000, 0x1FFFF);
        sb.Append(char.ConvertFromUtf32(codepoint));
        }
        return sb.ToString();
    }

    private static string GenerateZeroWidthChars()
    {
        int length = _random.Next(10, 5000);
        var sb = new StringBuilder(length * 3);
        var zwChars = new char[]
        {
            '\u200B', // Zero Width Space
            '\u200C', // Zero Width Non-Joiner
            '\u200D', // Zero Width Joiner
            '\uFEFF', // Zero Width No-Break Space (BOM)
            '\u2060', // Word Joiner
            '\u180E', // Mongolian Vowel Separator
        };
        for (int i = 0; i < length; i++)
        {
            sb.Append((char)_random.Next(32, 127));
            if (_random.Next(3) == 0)
            {
                sb.Append(zwChars[_random.Next(zwChars.Length)]);
            }
        }
        return sb.ToString();
    }

    private static string GenerateRtlMarkers()
    {
        int length = _random.Next(10, 3000);
        var sb = new StringBuilder(length * 3);
        var rtlChars = new char[]
        {
            '\u200E', // Left-to-Right Mark
            '\u200F', // Right-to-Left Mark
            '\u202A', // Left-to-Right Embedding
            '\u202B', // Right-to-Left Embedding
            '\u202C', // Pop Directional Formatting
            '\u202D', // Left-to-Right Override
            '\u202E', // Right-to-Left Override
        };
        for (int i = 0; i < length; i++)
        {
            sb.Append((char)_random.Next(32, 127));
            if (_random.Next(4) == 0)
            {
                sb.Append(rtlChars[_random.Next(rtlChars.Length)]);
            }
        }
        return sb.ToString();
    }

    private static string GenerateControlChars()
    {
        int length = _random.Next(10, 5000);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            if (_random.Next(5) == 0)
            {
                // Control characters (except \r, \n, \t which have special meaning)
                chars[i] = (char)_random.Next(0, 32);
                if (chars[i] == '\r' || chars[i] == '\n' || chars[i] == '\t')
                    chars[i] = (char)_random.Next(1, 8);
            }
            else
            {
                chars[i] = (char)_random.Next(32, 127);
            }
        }
        return new string(chars);
    }

    private static string GenerateNullBytes()
    {
        int length = _random.Next(10, 5000);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            if (_random.Next(20) == 0)
            {
                chars[i] = '\0';
            }
            else
            {
                chars[i] = (char)_random.Next(32, 127);
            }
        }
        return new string(chars);
    }

    private static string GenerateMixedLineEndings()
    {
        int lines = _random.Next(10, 500);
        var sb = new StringBuilder(lines * 50);
        var lineEndings = new string[] { "\r\n", "\n", "\r", "\u2028", "\u2029", "\v", "\f" };
        for (int i = 0; i < lines; i++)
        {
            int fieldCount = _random.Next(1, 10);
            for (int j = 0; j < fieldCount; j++)
            {
                sb.Append(RandomString(_random.Next(1, 30)));
                if (j < fieldCount - 1) sb.Append(',');
            }
            sb.Append(lineEndings[_random.Next(lineEndings.Length)]);
        }
        return sb.ToString();
    }

    private static string GenerateBomVariants()
    {
        var sb = new StringBuilder();
        // UTF-8 BOM
        if (_random.Next(2) == 0)
        {
            sb.Append('\uFEFF');
        }
        sb.Append(RandomString(_random.Next(100, 5000)));
        // Embed BOM in the middle
        if (_random.Next(3) == 0)
        {
            int pos = _random.Next(1, sb.Length);
            sb.Insert(pos, '\uFEFF');
        }
        return sb.ToString();
    }

    private static string GenerateLongFields()
    {
        int fieldLength = _random.Next(10000, 1000000);
        var sb = new StringBuilder(fieldLength + 50);
        for (int i = 0; i < fieldLength; i++)
        {
            sb.Append((char)_random.Next(32, 127));
        }
        // Add a few more fields
        for (int i = 0; i < _random.Next(1, 5); i++)
        {
            sb.Append(',');
            sb.Append(RandomString(_random.Next(1, 100)));
        }
        return sb.ToString();
    }

    private static string GenerateEmptyAndWhitespace()
    {
        var sb = new StringBuilder();
        int lines = _random.Next(10, 200);
        for (int i = 0; i < lines; i++)
        {
            switch (_random.Next(6))
            {
                case 0: sb.AppendLine(""); break; // Empty line
                case 1: sb.AppendLine("   "); break; // Spaces only
                case 2: sb.AppendLine("\t\t\t"); break; // Tabs only
                case 3: sb.AppendLine(",,,"); break; // Delimiters only
                case 4: sb.AppendLine(" , , "); break; // Mixed
                case 5: sb.AppendLine(new string(' ', _random.Next(1, 100))); break; // Variable spaces
            }
        }
        return sb.ToString();
    }

    private static string GenerateQuotedEdgeCases()
    {
        int lines = _random.Next(10, 300);
        var sb = new StringBuilder();
        for (int i = 0; i < lines; i++)
        {
            int fields = _random.Next(1, 8);
            for (int j = 0; j < fields; j++)
            {
                if (_random.Next(3) == 0)
                {
                    // Quoted field
                    sb.Append('"');
                    int contentLength = _random.Next(0, 200);
                    for (int k = 0; k < contentLength; k++)
                    {
                        if (_random.Next(10) == 0)
                        {
                            sb.Append("\"\""); // Escaped quote
                        }
                        else
                        {
                            sb.Append((char)_random.Next(32, 127));
                        }
                    }
                    // Sometimes don't close the quote
                    if (_random.Next(10) != 0)
                    {
                        sb.Append('"');
                    }
                }
                else
                {
                    sb.Append(RandomString(_random.Next(0, 50)));
                }
                if (j < fields - 1) sb.Append(',');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateDelimiterChaos()
    {
        int lines = _random.Next(10, 300);
        var sb = new StringBuilder();
        var delimiters = new char[] { ',', ';', '\t', '|', ':', ' ', '.' };
        for (int i = 0; i < lines; i++)
        {
            int fields = _random.Next(1, 15);
            char delim = delimiters[_random.Next(delimiters.Length)];
            for (int j = 0; j < fields; j++)
            {
                sb.Append(RandomString(_random.Next(0, 50)));
                if (j < fields - 1) sb.Append(delim);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateNestedQuotes()
    {
        int lines = _random.Next(10, 200);
        var sb = new StringBuilder();
        for (int i = 0; i < lines; i++)
        {
            // Nested quotes: "field "with "quotes" inside""
            sb.Append('"');
            int segments = _random.Next(1, 5);
            for (int s = 0; s < segments; s++)
            {
                sb.Append(RandomString(_random.Next(0, 30)));
                if (_random.Next(2) == 0)
                {
                    sb.Append("\"\"");
                }
                else
                {
                    sb.Append('"');
                    sb.Append(RandomString(_random.Next(0, 10)));
                    sb.Append('"');
                }
            }
            sb.Append('"');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateEscapedChaos()
    {
        int length = _random.Next(100, 5000);
        var sb = new StringBuilder(length * 2);
        for (int i = 0; i < length; i++)
        {
            if (_random.Next(5) == 0)
            {
                sb.Append("\\\"");
            }
            else if (_random.Next(5) == 0)
            {
                sb.Append("\\\\");
            }
            else if (_random.Next(10) == 0)
            {
                sb.Append("\\n");
            }
            else if (_random.Next(10) == 0)
            {
                sb.Append("\\t");
            }
            else
            {
                sb.Append((char)_random.Next(32, 127));
            }
        }
        return sb.ToString();
    }

    private static string GenerateNormalizationEdgeCases()
    {
        var sb = new StringBuilder();
        int lines = _random.Next(10, 200);
        for (int i = 0; i < lines; i++)
        {
            // Mix composed and decomposed characters
            // é can be U+00E9 (composed) or U+0065 U+0301 (decomposed)
            if (_random.Next(2) == 0)
            {
                sb.Append('\u00E9'); // é composed
            }
            else
            {
                sb.Append('e');
                sb.Append('\u0301'); // combining acute accent
            }

            // ñ can be U+00F1 (composed) or U+006E U+0303 (decomposed)
            if (_random.Next(2) == 0)
            {
                sb.Append('\u00F1'); // ñ composed
            }
            else
            {
                sb.Append('n');
                sb.Append('\u0303'); // combining tilde
            }

            sb.Append(',');
            sb.AppendLine(RandomString(_random.Next(0, 50)));
        }
        return sb.ToString();
    }

    private static string GenerateTruncatedUtf8()
    {
        // Generate valid UTF-16 string that might have been truncated from UTF-8
        int length = _random.Next(10, 3000);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            // High surrogates without matching low surrogates
            if (_random.Next(20) == 0)
            {
                chars[i] = (char)_random.Next(0xD800, 0xDC00); // High surrogate alone
            }
            else if (_random.Next(20) == 0)
            {
                chars[i] = (char)_random.Next(0xDC00, 0xE000); // Low surrogate alone
            }
            else
            {
                chars[i] = (char)_random.Next(32, 127);
            }
        }
        return new string(chars);
    }

    private static string GenerateRepeatingPatterns()
    {
        int patternLength = _random.Next(1, 50);
        var pattern = RandomString(patternLength);
        int repeatCount = _random.Next(100, 10000);
        var sb = new StringBuilder(pattern.Length * repeatCount + 100);
        for (int i = 0; i < repeatCount; i++)
        {
            sb.Append(pattern);
        }
        // Add some variation at the end
        sb.AppendLine();
        sb.Append(RandomString(_random.Next(1, 100)));
        return sb.ToString();
    }

    private static string GenerateMaxWidthFields()
    {
        // Generate fields at common buffer boundary sizes
        int[] boundarySizes = { 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536 };
        int size = boundarySizes[_random.Next(boundarySizes.Length)];
        var sb = new StringBuilder(size + 200);
        for (int i = 0; i < size; i++)
        {
            sb.Append('A');
        }
        sb.Append(',');
        sb.Append(RandomString(_random.Next(1, 100)));
        return sb.ToString();
    }

    private static string GenerateRandomBytesAsUtf8()
    {
        // Generate random bytes and interpret as UTF-8 string
        int length = _random.Next(100, 10000);
        var bytes = new byte[length];
        _random.NextBytes(bytes);
        // Replace invalid UTF-8 sequences with replacement char approach
        // Just create a string from bytes, handling invalid sequences
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)(bytes[i] & 0x7F); // Keep ASCII range to avoid invalid chars
        }
        return new string(chars);
    }

    private static string RandomString(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)_random.Next(32, 127);
        }
        return new string(chars);
    }

    private static string Sanitize(string input, int maxLength)
    {
        if (input.Length > maxLength)
            input = input.Substring(0, maxLength);
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
            {
                sb.Append("\\x");
                sb.Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
