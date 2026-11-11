using System.Buffers;
using System.Runtime.CompilerServices;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// The G2 prototype (design 3.1, gate G2): a hand-written <c>Format</c> for <see cref="PackageAsset"/> that takes one
/// <c>GetSpan(4096)</c>, writes all 25 fields into it and calls <c>Advance</c> once.
/// </summary>
/// <remarks>
/// <para>
/// It writes the same text as the generated <c>Format</c>: the converters the generator picks
/// (<see cref="Int32Converter"/>, <see cref="Int64Converter"/>, <see cref="BoolConverter"/>, and a
/// <see cref="DateTimeConverter"/> with the format <c>"s"</c> of <c>[FieldConverter(ConverterKind.Date, "s")]</c>),
/// called directly with <c>TryFormat(value, freeSpan, out n)</c> instead of through a <c>stackalloc</c> buffer and the
/// generic helper; string fields copied as they are (no quoting, as for the generated unquoted fields; a
/// <see langword="null"/> string writes nothing); a <c>','</c> between fields. Generated code would reach the converters
/// through <c>RecordWriter.Format&lt;TConverter, TValue&gt;</c> (design 5.1); the direct call is the fastest form, so
/// the prototype shows the most G2 can give.
/// </para>
/// <para>
/// When the record does not fit, nothing has been advanced, so it asks for a span twice as large and writes the
/// record again (never with this dataset, whose records are about 250 characters).
/// </para>
/// <para>
/// Only <c>Format</c> is hand-written; the rest delegates to the generated descriptor, and <c>Parse</c> is not
/// supported. <see cref="PrototypeEngine"/> runs it in the engine's write path.
/// </para>
/// </remarks>
internal sealed class PackageAssetSpanFormat : IRecordDescriptor<PackageAsset>
{
    private const int FirstSpan = 4096;

    private static readonly IRecordDescriptor<PackageAsset> s_generated = FileHelperRegistry<PackageAsset>.Descriptor;
    private static readonly DateTimeConverter s_date = new(DatasetGenerator.DateFormat);

    public void Format(PackageAsset record, IBufferWriter<char> writer)
    {
        int size = FirstSpan;
        while (true)
        {
            Span<char> span = writer.GetSpan(size);
            if (TryWrite(record, span, out int written))
            {
                writer.Advance(written);
                return;
            }
            if (span.Length >= 1 << 30)
                throw new FormatException("The record could not be formatted.");
            size = span.Length * 2;
        }
    }

    private static bool TryWrite(PackageAsset r, Span<char> d, out int written)
    {
        int p = 0;
        bool ok =
            Int(r.Id, d, ref p) && Comma(d, ref p)
            && Text(r.Version, d, ref p) && Comma(d, ref p)
            && Text(r.Authors, d, ref p) && Comma(d, ref p)
            && Text(r.Description, d, ref p) && Comma(d, ref p)
            && Text(r.Tags, d, ref p) && Comma(d, ref p)
            && Int(r.DownloadCount, d, ref p) && Comma(d, ref p)
            && Text(r.ProjectUrl, d, ref p) && Comma(d, ref p)
            && Text(r.LicenseUrl, d, ref p) && Comma(d, ref p)
            && Text(r.IconUrl, d, ref p) && Comma(d, ref p)
            && Text(r.Language, d, ref p) && Comma(d, ref p)
            && Text(r.Framework, d, ref p) && Comma(d, ref p)
            && Text(r.MinClientVersion, d, ref p) && Comma(d, ref p)
            && Int(r.Type, d, ref p) && Comma(d, ref p)
            && Bool(r.IsPrerelease, d, ref p) && Comma(d, ref p)
            && Date(r.Published, d, ref p) && Comma(d, ref p)
            && Date(r.LastUpdated, d, ref p) && Comma(d, ref p)
            && Long(r.PackageSize, d, ref p) && Comma(d, ref p)
            && Int(r.DependencyCount, d, ref p) && Comma(d, ref p)
            && Bool(r.HasReadme, d, ref p) && Comma(d, ref p)
            && Bool(r.HasLicense, d, ref p) && Comma(d, ref p)
            && Bool(r.HasIcon, d, ref p) && Comma(d, ref p)
            && Bool(r.IsSigned, d, ref p) && Comma(d, ref p)
            && Text(r.RepositoryType, d, ref p) && Comma(d, ref p)
            && Text(r.RepositoryUrl, d, ref p) && Comma(d, ref p)
            && Bool(r.RequireLicenseAcceptance, d, ref p);
        written = p;
        return ok;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Comma(Span<char> d, ref int p)
    {
        if ((uint)p >= (uint)d.Length)
            return false;
        d[p++] = ',';
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Text(string? value, Span<char> d, ref int p)
    {
        ReadOnlySpan<char> text = value;
        if (!text.TryCopyTo(d.Slice(p)))
            return false;
        p += text.Length;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Int(int value, Span<char> d, ref int p)
    {
        if (!Int32Converter.Instance.TryFormat(value, d.Slice(p), out int n))
            return false;
        p += n;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Long(long value, Span<char> d, ref int p)
    {
        if (!Int64Converter.Instance.TryFormat(value, d.Slice(p), out int n))
            return false;
        p += n;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Bool(bool value, Span<char> d, ref int p)
    {
        if (!BoolConverter.Instance.TryFormat(value, d.Slice(p), out int n))
            return false;
        p += n;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Date(DateTime value, Span<char> d, ref int p)
    {
        if (!s_date.TryFormat(value, d.Slice(p), out int n))
            return false;
        p += n;
        return true;
    }

    public PackageAsset Parse(ReadOnlySpan<char> lines, int lineCount) =>
        throw new NotSupportedException("The G2 prototype only formats.");

    public ReadOnlySpan<FieldDescriptor> Fields => s_generated.Fields;

    public RecordOptions Options => s_generated.Options;

    public PackageAsset CreateDefaultInstance() => new();
}
