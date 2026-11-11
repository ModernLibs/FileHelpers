using System.Buffers;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// The R6 upper bound (design 3.1, gate R6): a hand-written descriptor for <see cref="QuotedRecord"/> whose
/// <c>Parse</c> takes the field ends of each record from an array computed before the read, so it pays nothing for
/// tokenizing. It only slices, unquotes, converts and constructs.
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
/// Unquoting is the fastest correct code for the flags: a quoted field without a doubled quote is one
/// <c>new string</c> of its inner text; with one, a single <c>string.Create</c> of the final length copies the blocks
/// between quotes (no rented buffer, unlike the emitted strip pass). The bound therefore also includes the gain of
/// R3 on these fields. The <c>Id</c> field is converted with <see cref="Int32Converter.Instance"/>, as the generated
/// code does; unquoted text fields are <c>new string</c>, as the generated code does (R4 is not part of the bound).
/// </para>
/// </remarks>
internal sealed class QuotedPrecomputedEndsDescriptor : IRecordDescriptor<QuotedRecord>
{
    private const int FieldsPerRow = 5;
    private const int QuotedFlag = 1 << 30;
    private const int DoubledFlag = 1 << 29;
    private const int EndMask = DoubledFlag - 1;
    private const char Quote = '"';

    private static readonly IRecordDescriptor<QuotedRecord> s_generated = FileHelperRegistry<QuotedRecord>.Descriptor;

    private readonly int[] _ends;
    private int _row;

    private QuotedPrecomputedEndsDescriptor(int[] ends) => _ends = ends;

    /// <summary>The number of records parsed since the last <see cref="Rewind"/>.</summary>
    public int RowsParsed => _row;

    /// <summary>Starts again at the first row; call it before each read.</summary>
    public void Rewind() => _row = 0;

    /// <summary>
    /// Computes the field ends of every data line of <paramref name="path"/> (a file of the seeded quoted set, header
    /// skipped as <see cref="QuotedRecord"/>'s <c>[IgnoreFirst]</c> does) with <see cref="ComponentData.QuotedFields"/>,
    /// a reference splitter that does not use the library's scanner.
    /// </summary>
    public static QuotedPrecomputedEndsDescriptor FromFile(string path)
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
        return new QuotedPrecomputedEndsDescriptor(ends);
    }

    public QuotedRecord Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        if (lineCount != 1)
            throw new BadUsageException($"Expected 1 lines for the record but received {lineCount}. Check that all multi-line fields are present.");

        int b = _row * FieldsPerRow;
        int[] ends = _ends;
        if ((uint)(b + FieldsPerRow) > (uint)ends.Length)
            throw new InvalidOperationException($"R6 upper bound: no precomputed ends for row {_row}.");
        int e0 = ends[b], e1 = ends[b + 1], e2 = ends[b + 2], e3 = ends[b + 3], e4 = ends[b + 4];
        if ((e4 & EndMask) != lines.Length)
            throw new InvalidOperationException($"R6 upper bound: the precomputed ends of row {_row} do not belong to this record.");
        _row++;

        int end0 = e0 & EndMask;
        if (!Int32Converter.Instance.TryParse(lines.Slice(0, end0), out int id))
            throw new InvalidOperationException($"R6 upper bound: '{lines.Slice(0, end0)}' is not an Id.");

        int end1 = e1 & EndMask, end2 = e2 & EndMask, end3 = e3 & EndMask;
        return new QuotedRecord
        {
            Id = id,
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

    public void Format(QuotedRecord record, IBufferWriter<char> writer) => s_generated.Format(record, writer);

    public ReadOnlySpan<FieldDescriptor> Fields => s_generated.Fields;

    public RecordOptions Options => s_generated.Options;

    public QuotedRecord CreateDefaultInstance() => new();
}
