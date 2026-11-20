using System.Globalization;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks;

namespace ModernLibs.FileHelpers.Benchmarks.Aot;

/// <summary>
/// A checksum of every field of every PackageAssets row, in file order: 64-bit FNV-1a over the parsed values
/// (integers, the ticks of the dates, booleans as 0 or 1) and over the length and characters of every string.
/// <see cref="OfFile"/> computes it from the text with the BCL parsers; <see cref="OfRecords"/> from the records the
/// library reads. They agree only when the library read every field of every row correctly, and two builds agree
/// only when they read the same values.
/// </summary>
internal static class DataChecksum
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private const int Columns = 25;

    // Column kinds of the PackageAssets file, in file order (see PackageAsset).
    // i int, l long, b bool, d date ("s"), s string.
    private const string Kinds = "issssissssssibddlibbbbssb";

    public static (ulong Checksum, int Rows) OfFile(string path)
    {
        ulong h = OffsetBasis;
        int rows = 0;
        using var reader = new StreamReader(path);
        _ = reader.ReadLine(); // header line
        Span<Range> ranges = stackalloc Range[Columns + 1];
        while (reader.ReadLine() is { } line)
        {
            var span = line.AsSpan();
            int count = span.Split(ranges, ',');
            if (count != Columns)
                throw new InvalidDataException($"{path}: data row {rows} has {count} fields, expected {Columns}");
            for (int i = 0; i < Columns; i++)
                h = AddField(h, Kinds[i], span[ranges[i]], path, rows, i);
            rows++;
        }
        return (h, rows);
    }

    private static ulong AddField(ulong h, char kind, ReadOnlySpan<char> field, string path, int row, int column)
    {
        try
        {
            return kind switch
            {
                'i' => Add(h, int.Parse(field, NumberStyles.Integer, CultureInfo.InvariantCulture)),
                'l' => Add(h, long.Parse(field, NumberStyles.Integer, CultureInfo.InvariantCulture)),
                'b' => Add(h, bool.Parse(field)),
                'd' => Add(h, DateTime.ParseExact(field, "s", CultureInfo.InvariantCulture).Ticks),
                _ => Add(h, field),
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new InvalidDataException($"{path}: data row {row}, column {column + 1}: {ex.Message}", ex);
        }
    }

    public static (ulong Checksum, int Rows) OfRecords(FileHelperEngine<PackageAsset> engine, string path)
    {
        ulong h = OffsetBasis;
        int rows = 0;
        engine.ReadFile(path, r =>
        {
            h = Add(h, r);
            rows++;
        });
        return (h, rows);
    }

    private static ulong Add(ulong h, PackageAsset r)
    {
        h = Add(h, r.Id);
        h = Add(h, r.Version);
        h = Add(h, r.Authors);
        h = Add(h, r.Description);
        h = Add(h, r.Tags);
        h = Add(h, r.DownloadCount);
        h = Add(h, r.ProjectUrl);
        h = Add(h, r.LicenseUrl);
        h = Add(h, r.IconUrl);
        h = Add(h, r.Language);
        h = Add(h, r.Framework);
        h = Add(h, r.MinClientVersion);
        h = Add(h, r.Type);
        h = Add(h, r.IsPrerelease);
        h = Add(h, r.Published.Ticks);
        h = Add(h, r.LastUpdated.Ticks);
        h = Add(h, r.PackageSize);
        h = Add(h, r.DependencyCount);
        h = Add(h, r.HasReadme);
        h = Add(h, r.HasLicense);
        h = Add(h, r.HasIcon);
        h = Add(h, r.IsSigned);
        h = Add(h, r.RepositoryType);
        h = Add(h, r.RepositoryUrl);
        return Add(h, r.RequireLicenseAcceptance);
    }

    private static ulong Add(ulong h, long value) => (h ^ (ulong)value) * Prime;

    private static ulong Add(ulong h, bool value) => Add(h, value ? 1L : 0L);

    private static ulong Add(ulong h, ReadOnlySpan<char> value)
    {
        h = Add(h, value.Length);
        foreach (char c in value)
            h = (h ^ c) * Prime;
        return h;
    }
}
