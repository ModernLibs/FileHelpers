// MA0048: Fixture types for the read-core tests are grouped in one file.
#pragma warning disable MA0048

using System.Buffers;
using System.Runtime.InteropServices;
using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>Plain two-field class record.</summary>
[DelimitedRecord(",")]
public sealed class RcRecord
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Two-field struct record (allocation-free path, E2 end-of-file detection).</summary>
[DelimitedRecord(",")]
[StructLayout(LayoutKind.Auto)]
public record struct RcStruct
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public int Value { get; set; }
}

/// <summary>Header, footer, empty and comment filters on one record.</summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(2)]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
public sealed class RcFramed
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Footer only, struct (E4 with struct records).</summary>
[DelimitedRecord(",")]
[IgnoreLast(1)]
[StructLayout(LayoutKind.Auto)]
public record struct RcFooterStruct
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public int Value { get; set; }
}

/// <summary>Multiline quoted field (read).</summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public sealed class RcMultiline
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>Multiline quoted field with <see cref="MultilineMode.AllowForBoth"/> (P16).</summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForBoth)]
public sealed class RcMultilineBoth
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Multiline record mixing a trimmed quoted field (blanks allowed before the quote) and an untrimmed one
/// (a quote after blanks is literal), as in FileHelpers 3.x.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForBoth)]
public sealed class RcMultilineTrimmed
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] [FieldTrim(TrimMode.Both)] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldQuoted] public string Tail { get; set; } = string.Empty;
}

/// <summary>Multiline plus empty/comment filters (E7).</summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
public sealed class RcMultilineFiltered
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

/// <summary>Unquoted three-field record whose data may contain a literal quote (P4).</summary>
[DelimitedRecord(",")]
public sealed class RcInch
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Desc { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tag { get; set; } = string.Empty;
}

/// <summary>Same as <see cref="RcInch"/> with multiline reading enabled (P4).</summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public sealed class RcInchMultiline
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Desc { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tag { get; set; } = string.Empty;
}

/// <summary>Regex condition (P14: 3.x matches case-insensitively).</summary>
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfRegex, "^abc")]
public sealed class RcRegexInclude
{
    [FieldOrder(1)] public string Code { get; set; } = string.Empty;
    [FieldOrder(2)] public int Value { get; set; }
}

/// <summary>Class record whose generated descriptor supports recycling (E11).</summary>
[DelimitedRecord(",")]
public sealed class RcRecyclable
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Hand-written descriptor for options the generator does not emit yet (<see cref="RecordOptions.QuoteChar"/>,
/// <see cref="RecordOptions.IgnoreEmptySpaces"/>). Fields: <c>A</c>, <c>B</c> (strings); a field starting with
/// the quote char is unquoted with doubled quotes collapsed.
/// </summary>
public sealed class HandRecord<TTag>
{
    public string A { get; set; } = string.Empty;
    public string B { get; set; } = string.Empty;
}

/// <summary>Tag: apostrophe quote char, multiline read.</summary>
public sealed class ApostropheQuoteTag;

/// <summary>Tag: IgnoreEmptyLines with IgnoreSpaces.</summary>
public sealed class IgnoreSpacesTag;

internal sealed class HandDescriptor<TTag>(RecordOptions options) : IRecordDescriptor<HandRecord<TTag>>
{
    private static readonly FieldDescriptor[] s_fields =
    [
        new("A", typeof(string), 0),
        new("B", typeof(string), 1),
    ];

    public ReadOnlySpan<FieldDescriptor> Fields => s_fields;

    public RecordOptions Options => options;

    public HandRecord<TTag> Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        char quote = options.QuoteChar == '\0' ? '"' : options.QuoteChar;
        var values = new List<string>(2);
        int i = 0;
        while (i <= lines.Length)
        {
            if (i < lines.Length && lines[i] == quote)
            {
                var sb = new System.Text.StringBuilder();
                int j = i + 1;
                while (j < lines.Length)
                {
                    if (lines[j] == quote)
                    {
                        if (j + 1 < lines.Length && lines[j + 1] == quote)
                        {
                            sb.Append(quote);
                            j += 2;
                            continue;
                        }

                        break;
                    }

                    sb.Append(lines[j]);
                    j++;
                }

                values.Add(sb.ToString());
                i = j + 2; // closing quote + delimiter
            }
            else
            {
                int end = lines[i..].IndexOf(',');
                if (end < 0)
                {
                    values.Add(lines[i..].ToString());
                    break;
                }

                values.Add(lines.Slice(i, end).ToString());
                i += end + 1;
            }
        }

        return new HandRecord<TTag> { A = values.Count > 0 ? values[0] : string.Empty, B = values.Count > 1 ? values[1] : string.Empty };
    }

    public void Format(HandRecord<TTag> record, IBufferWriter<char> writer)
    {
        var text = record.A + "," + record.B;
        var span = writer.GetSpan(text.Length);
        text.AsSpan().CopyTo(span);
        writer.Advance(text.Length);
    }

    public HandRecord<TTag> CreateDefaultInstance() => new();
}

/// <summary>Registers the hand-written descriptors exactly once.</summary>
internal static class HandDescriptors
{
    private static readonly Lock s_gate = new();
    private static bool s_registered;

    public static void EnsureRegistered()
    {
        lock (s_gate)
        {
            if (s_registered)
                return;

            Register<HandRecord<ApostropheQuoteTag>>(new HandDescriptor<ApostropheQuoteTag>(new RecordOptions
            {
                Delimiter = ",",
                ConditionPattern = string.Empty,
                LineCountPerLogicalRecord = 1,
                MultilineMode = MultilineMode.AllowForRead,
                QuoteChar = '\'',
            }));
            Register<HandRecord<IgnoreSpacesTag>>(new HandDescriptor<IgnoreSpacesTag>(new RecordOptions
            {
                Delimiter = ",",
                ConditionPattern = string.Empty,
                LineCountPerLogicalRecord = 1,
                MultilineMode = MultilineMode.NotAllow,
                IgnoreEmptyLines = true,
                IgnoreEmptySpaces = true,
            }));
            s_registered = true;
        }
    }

    /// <summary>Creates an engine for a hand-registered type (generic indirection avoids MLFH001 on undecorated types).</summary>
    public static FileHelperEngine<TRecord> NewEngine<TRecord>()
    {
        EnsureRegistered();
        return new FileHelperEngine<TRecord>();
    }

    /// <summary>Creates a streaming engine for a hand-registered type.</summary>
    public static FileHelperAsyncEngine<TRecord> NewAsyncEngine<TRecord>()
    {
        EnsureRegistered();
        return new FileHelperAsyncEngine<TRecord>();
    }

    private static void Register<TRecord>(IRecordDescriptor<TRecord> descriptor) =>
        FileHelperRegistry<TRecord>.SetDescriptor(descriptor);
}
