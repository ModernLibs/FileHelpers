// MA0051: the field-by-field mappings are long on purpose.
#pragma warning disable MA0051

using System.Globalization;
using FlameCsv.Attributes;
using RecordParser.Builders.Reader;
using RecordParser.Parsers;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// FlameCsv generates its binding at compile time from these declarations (the same approach as the
// [CsvHelper.CsvRecord] rows); the columns are bound by header name. The file is read as UTF-8 bytes, the
// way FlameCsv is meant to read a file.
[CsvTypeMap<byte, PackageAssetRow>]
internal sealed partial class PackageAssetFlameMap;

[CsvTypeMap<byte, QuotedCompetitorRow>]
internal sealed partial class QuotedFlameMap;

/// <summary>RecordParser reader definitions for the datasets. RecordParser maps by column index and builds its reader once.</summary>
internal static class RecordParserReaders
{
    public static IVariableLengthReader<PackageAssetRow> PackageAssets() => new VariableLengthReaderBuilder<PackageAssetRow>()
        .Map(x => x.Id, 0)
        .Map(x => x.Version, 1)
        .Map(x => x.Authors, 2)
        .Map(x => x.Description, 3)
        .Map(x => x.Tags, 4)
        .Map(x => x.DownloadCount, 5)
        .Map(x => x.ProjectUrl, 6)
        .Map(x => x.LicenseUrl, 7)
        .Map(x => x.IconUrl, 8)
        .Map(x => x.Language, 9)
        .Map(x => x.Framework, 10)
        .Map(x => x.MinClientVersion, 11)
        .Map(x => x.Type, 12)
        .Map(x => x.IsPrerelease, 13)
        .Map(x => x.Published, 14)
        .Map(x => x.LastUpdated, 15)
        .Map(x => x.PackageSize, 16)
        .Map(x => x.DependencyCount, 17)
        .Map(x => x.HasReadme, 18)
        .Map(x => x.HasLicense, 19)
        .Map(x => x.HasIcon, 20)
        .Map(x => x.IsSigned, 21)
        .Map(x => x.RepositoryType, 22)
        .Map(x => x.RepositoryUrl, 23)
        .Map(x => x.RequireLicenseAcceptance, 24)
        .Build(",", CultureInfo.InvariantCulture, () => new PackageAssetRow());

    public static IVariableLengthReader<QuotedCompetitorRow> Quoted() => new VariableLengthReaderBuilder<QuotedCompetitorRow>()
        .Map(x => x.Id, 0)
        .Map(x => x.Name, 1)
        .Map(x => x.Description, 2)
        .Map(x => x.Notes, 3)
        .Map(x => x.Category, 4)
        .Build(",", CultureInfo.InvariantCulture, () => new QuotedCompetitorRow());

    // Fields keep their padding (no trim), like FixedRecord.
    public static IFixedLengthReader<FixedRecord> Fixed() => new FixedLengthReaderBuilder<FixedRecord>()
        .Map(x => x.Id, 0, 8)
        .Map(x => x.Name, 8, 12)
        .Map(x => x.NumericField, 20, 10)
        .Map(x => x.Description, 30, 15)
        .Map(x => x.DecimalField, 45, 20)
        .Map(x => x.Code, 65, 5)
        .Map(x => x.Suffix, 70, 10)
        .Build(CultureInfo.InvariantCulture, () => new FixedRecord());
}
