using System.Buffers;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// The R6 upper bound (design 3.1, gate R6): a hand-written descriptor for <see cref="QuotedRecord"/> whose
/// <c>Parse</c> takes the field ends of each record from an array computed before the read, so it pays nothing for
/// tokenizing. It only slices, unquotes, converts and constructs. Two variants differ only in how they unquote:
/// <see cref="QuotedPrecomputedEndsEmittedUnquote"/> (the gate) and <see cref="QuotedPrecomputedEndsFastUnquote"/>.
/// </summary>
/// <remarks>
/// <para>
/// Run through <see cref="PrototypeEngine"/>, the engine does everything else as for the generated descriptor: it
/// reads and splits the file, the multiline assembler scans each line for an open quote
/// (<c>RecordReadCore.OpensMultiline</c>; R6 keeps that scan and makes it record the ends), and the field-count check
/// reuses that scan. What disappears is the generated parser's second, quote-aware walk over the line. That is the
/// most R6 can give.
/// </para>
/// <para>
/// One <c>int</c> per field, five per row, in file order: the end of the field (exclusive, quotes included), with
/// bit 30 set when the field is quoted and bit 29 when its quoted text holds a doubled quote; the tokenizer of
/// design 4.5 records the same two flags. The descriptor consumes one row per <c>Parse</c> call: call
/// <see cref="Rewind"/> before each read. As a guard that costs one compare, it throws when the end of the last field
/// is not the length of the record it is given, so a read that is out of step with the array fails.
/// </para>
/// <para>
/// The array holds the ends of the whole file (2 MB on the large object heap at 100,000 rows; each record reads 20
/// bytes of it). A real tokenizer writes into one small buffer that it reuses, so the array costs the prototype a
/// little memory traffic that R6 would not have: a small effect against the bound.
/// </para>
/// <para>
/// The <c>Id</c> field is converted with <see cref="Int32Converter.Instance"/> and unquoted text fields are
/// <c>new string</c>, as the generated code does (R4 is not part of the bound).
/// </para>
/// </remarks>
internal abstract class QuotedPrecomputedEndsDescriptor : IRecordDescriptor<QuotedRecord>
{
    protected const int QuotedFlag = 1 << 30;
    protected const int DoubledFlag = 1 << 29;
    protected const int EndMask = DoubledFlag - 1;
    protected const char Quote = '"';
    private const int FieldsPerRow = 5;

    private static readonly IRecordDescriptor<QuotedRecord> s_generated = FileHelperRegistry<QuotedRecord>.Descriptor;

    private readonly int[] _ends;
    private int _row;

    /// <summary>
    /// Computes the field ends of every data line of <paramref name="path"/> (a file of the seeded quoted set, header
    /// skipped as <see cref="QuotedRecord"/>'s <c>[IgnoreFirst]</c> does) with <see cref="ComponentData.QuotedFields"/>,
    /// a reference splitter that does not use the library's scanner.
    /// </summary>
    protected QuotedPrecomputedEndsDescriptor(string path)
    {
        string[] lines = DataCheck.DataLines(path, hasHeader: true);
        var ends = new int[lines.Length * FieldsPerRow];
        for (int row = 0; row < lines.Length; row++)
        {
            string line = lines[row];
            var fields = ComponentData.QuotedFields(line);
            DataCheck.Equal(FieldsPerRow, fields.Count, $"R6 precomputed ends, row {row} field count");
            for (int k = 0; k < FieldsPerRow; k++)
            {
                var (start, end, quoted) = fields[k];
                DataCheck.That(end <= EndMask, $"R6 precomputed ends, row {row}: line too long");
                int packed = end;
                if (quoted)
                {
                    packed |= QuotedFlag;
                    if (line.AsSpan(start + 1, end - start - 2).Contains(Quote))
                        packed |= DoubledFlag;
                }
                ends[row * FieldsPerRow + k] = packed;
            }
        }
        _ends = ends;
    }

    /// <summary>The number of records parsed since the last <see cref="Rewind"/>.</summary>
    public int RowsParsed => _row;

    /// <summary>Starts again at the first row; call it before each read.</summary>
    public void Rewind() => _row = 0;

    public abstract QuotedRecord Parse(ReadOnlySpan<char> lines, int lineCount);

    /// <summary>The five packed ends of the next row, after the line-count check and the guard; advances the row.</summary>
    protected (int E0, int E1, int E2, int E3, int E4) NextRow(ReadOnlySpan<char> lines, int lineCount)
    {
        if (lineCount != 1)
            throw new BadUsageException($"Expected 1 lines for the record but received {lineCount}. Check that all multi-line fields are present.");

        int b = _row * FieldsPerRow;
        int[] ends = _ends;
        if ((uint)(b + FieldsPerRow) > (uint)ends.Length)
            throw new InvalidOperationException($"R6 upper bound: no precomputed ends for row {_row}.");
        int e4 = ends[b + 4];
        if ((e4 & EndMask) != lines.Length)
            throw new InvalidOperationException($"R6 upper bound: the precomputed ends of row {_row} do not belong to this record.");
        _row++;
        return (ends[b], ends[b + 1], ends[b + 2], ends[b + 3], e4);
    }

    /// <summary>The <c>Id</c> field, converted as the generated code does.</summary>
    protected static int ParseId(ReadOnlySpan<char> field)
    {
        if (!Int32Converter.Instance.TryParse(field, out int id))
            throw new InvalidOperationException($"R6 upper bound: '{field}' is not an Id.");
        return id;
    }

    public void Format(QuotedRecord record, IBufferWriter<char> writer) => s_generated.Format(record, writer);

    public ReadOnlySpan<FieldDescriptor> Fields => s_generated.Fields;

    public RecordOptions Options => s_generated.Options;

    public QuotedRecord CreateDefaultInstance() => new();
}

/// <summary>
/// The R6 gate variant (<c>R6_PrecomputedEnds</c>, lead ruling R32): precomputed ends, then today's emitted strip
/// pass (<c>DescriptorEmitter.cs:1088-1128</c>) on every quoted field: a scalar search for a doubled quote and, on a
/// hit, a char-by-char copy into a rented buffer, all buffers returned in one <c>finally</c> per record after the
/// strings are made, as in the generated parser. It ignores the doubled-quote flag, which the emitted code does not
/// have. This is R6 alone, a little conservative, because the real tokenizer also hands over the flag and saves the
/// search.
/// </summary>
internal sealed class QuotedPrecomputedEndsEmittedUnquote(string path) : QuotedPrecomputedEndsDescriptor(path)
{
    public override QuotedRecord Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        var (e0, e1, e2, e3, e4) = NextRow(lines, lineCount);
        int end0 = e0 & EndMask, end1 = e1 & EndMask, end2 = e2 & EndMask, end3 = e3 & EndMask;

        char[]? rent1 = null;
        char[]? rent2 = null;
        char[]? rent3 = null;
        char[]? rent4 = null;
        try
        {
            var f0 = lines.Slice(0, end0);
            var f1 = Strip(lines, end0 + 1, e1, ref rent1);
            var f2 = Strip(lines, end1 + 1, e2, ref rent2);
            var f3 = Strip(lines, end2 + 1, e3, ref rent3);
            var f4 = Strip(lines, end3 + 1, e4, ref rent4);
            return new QuotedRecord
            {
                Id = ParseId(f0),
                Name = new string(f1),
                Description = new string(f2),
                Notes = new string(f3),
                Category = new string(f4),
            };
        }
        finally
        {
            if (rent1 is not null) ArrayPool<char>.Shared.Return(rent1);
            if (rent2 is not null) ArrayPool<char>.Shared.Return(rent2);
            if (rent3 is not null) ArrayPool<char>.Shared.Return(rent3);
            if (rent4 is not null) ArrayPool<char>.Shared.Return(rent4);
        }
    }

    /// <summary>The field's raw text or, when it is quoted, the result of today's emitted strip pass.</summary>
    private static ReadOnlySpan<char> Strip(ReadOnlySpan<char> line, int start, int packed, ref char[]? rent)
    {
        int end = packed & EndMask;
        var field = line.Slice(start, end - start);
        if ((packed & QuotedFlag) == 0)
            return field;

        var inner = field.Slice(1, field.Length - 2);
        bool hasDoubled = false;
        for (int i = 0; i < inner.Length - 1; i++)
        {
            if (inner[i] == Quote && inner[i + 1] == Quote)
            {
                hasDoubled = true;
                break;
            }
        }
        if (!hasDoubled)
            return inner;

        // Rented buffer, returned in the finally block after the value is materialised.
        var buffer = ArrayPool<char>.Shared.Rent(inner.Length);
        rent = buffer;
        int length = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == Quote && i + 1 < inner.Length && inner[i + 1] == Quote)
            {
                buffer[length++] = Quote;
                i++;
            }
            else
            {
                buffer[length++] = c;
            }
        }
        return new ReadOnlySpan<char>(buffer, 0, length);
    }
}

/// <summary>
/// The informative variant (<c>R6_PrecomputedEnds_FastUnquote</c>): precomputed ends and the fastest correct unquote
/// for the flags. A quoted field without a doubled quote is one <c>new string</c> of its inner text; with one, a single
/// <c>string.Create</c> of the final length copies the blocks between quotes (no search, no rented buffer). This is
/// R6 plus the gain of R3 on these fields.
/// </summary>
internal sealed class QuotedPrecomputedEndsFastUnquote(string path) : QuotedPrecomputedEndsDescriptor(path)
{
    public override QuotedRecord Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        var (e0, e1, e2, e3, e4) = NextRow(lines, lineCount);
        int end0 = e0 & EndMask, end1 = e1 & EndMask, end2 = e2 & EndMask, end3 = e3 & EndMask;
        return new QuotedRecord
        {
            Id = ParseId(lines.Slice(0, end0)),
            Name = Text(lines, end0 + 1, e1),
            Description = Text(lines, end1 + 1, e2),
            Notes = Text(lines, end2 + 1, e3),
            Category = Text(lines, end3 + 1, e4),
        };
    }

    private static string Text(ReadOnlySpan<char> line, int start, int packed)
    {
        int end = packed & EndMask;
        if ((packed & QuotedFlag) == 0)
            return new string(line.Slice(start, end - start));

        var inner = line.Slice(start + 1, end - start - 2);
        if ((packed & DoubledFlag) == 0)
            return new string(inner);

        // Inside a quoted value every quote is one of a doubled pair.
        return string.Create(inner.Length - (inner.Count(Quote) >> 1), inner, static (destination, source) =>
        {
            while (true)
            {
                int at = source.IndexOf(Quote);
                if (at < 0)
                {
                    source.CopyTo(destination);
                    return;
                }
                source.Slice(0, at + 1).CopyTo(destination);
                destination = destination.Slice(at + 1);
                source = source.Slice(at + 2);
            }
        });
    }
}
