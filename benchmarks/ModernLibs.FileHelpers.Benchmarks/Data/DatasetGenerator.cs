// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004

using System.Text;

namespace ModernLibs.FileHelpers.Benchmarks.Data;

/// <summary>Generates deterministic benchmark datasets.</summary>
public static class DatasetGenerator
{
    private static readonly Random s_rng = new(42); // deterministic seed

    public static string EnsurePackageAssets(string dataDir, int rows = 1_000_000)
    {
        var path = Path.Combine(dataDir, $"package_assets_{rows}.csv");
        if (File.Exists(path)) return path;

        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Version,Authors,Description,Tags,DownloadCount,ProjectUrl,LicenseUrl,IconUrl,Language,Framework,MinClientVersion,Type,IsPrerelease,Published,LastUpdated,PackageSize,DependencyCount,HasReadme,HasLicense,HasIcon,IsSigned,RepositoryType,RepositoryUrl,RequireLicenseAcceptance");

        var descriptions = new[] { "A useful library", "High-performance framework", "Simple utility", "Modern toolkit", "Core component" };
        var tags = new[] { "library;framework", "utility;tool", "core;runtime", "data;parsing", "async;streaming" };
        var authors = new[] { "Author A", "Author B", "Author C", "Team X", "Org Y" };
        var langs = new[] { "en-US", "en-GB", "de-DE", "fr-FR", "ja-JP" };
        var frameworks = new[] { "net10.0", "net8.0", "netstandard2.0", "net6.0", "net472" };
        var repoTypes = new[] { "git", "svn", "hg", "" };

        for (int i = 0; i < rows; i++)
        {
            var version = $"{s_rng.Next(0, 10)}.{s_rng.Next(0, 20)}.{s_rng.Next(0, 100)}";
            var downloads = s_rng.Next(100, 10_000_000);
            var isPrerelease = s_rng.Next(0, 10) == 0;
            if (isPrerelease) version += $"-beta.{s_rng.Next(1, 5)}";
            var pubDate = DateTime.UtcNow.AddDays(-s_rng.Next(0, 3650));
            var size = s_rng.Next(1000, 50_000_000);
            var depCount = s_rng.Next(0, 20);

            writer.WriteLine($"{i},{Escape(version)},{Escape(authors[i % authors.Length])},{Escape(descriptions[i % descriptions.Length])},{Escape(tags[i % tags.Length])},{downloads},https://example.com/{i},https://license.example.com/{i},https://icon.example.com/{i},{Escape(langs[i % langs.Length])},{Escape(frameworks[i % frameworks.Length])},,{s_rng.Next(0, 3)},{isPrerelease},{pubDate:O},{pubDate.AddDays(1):O},{size},{depCount},{s_rng.Next(0, 2) == 1},{s_rng.Next(0, 2) == 1},{s_rng.Next(0, 2) == 1},{s_rng.Next(0, 2) == 1},{Escape(repoTypes[i % repoTypes.Length])},https://repo.example.com/{i},{s_rng.Next(0, 2) == 1}");
        }

        return path;
    }

    public static string EnsureFloats(string dataDir, int rows = 100_000)
    {
        var path = Path.Combine(dataDir, $"floats_{rows}.csv");
        if (File.Exists(path)) return path;

        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Value1,Value2,Value3,Value4,Value5,Value6,Value7,Value8,Value9,Value10");

        for (int i = 0; i < rows; i++)
        {
            writer.WriteLine($"{i},{s_rng.NextDouble() * 1e9},{s_rng.NextDouble() * 1e6},{s_rng.NextDouble() * 1e3},{s_rng.NextDouble() * 100},{s_rng.NextDouble() * 10},{s_rng.NextDouble()},{s_rng.NextDouble() * 0.001},{s_rng.NextDouble() * 0.000001},{s_rng.NextDouble() * 1e-9},{s_rng.NextDouble() * 1e-12}");
        }

        return path;
    }

    public static string EnsureFixedWidth(string dataDir, int rows = 500_000)
    {
        var path = Path.Combine(dataDir, $"fixed_width_{rows}.txt");
        if (File.Exists(path)) return path;

        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);

        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        for (int i = 0; i < rows; i++)
        {
            // 80-char record: 8 + 12 + 10 + 15 + 20 + 5 + 10 = 80
            var sb = new StringBuilder(80);
            sb.Append(i.ToString("D8"));
            sb.Append(RandomString(chars, 12));
            sb.Append(s_rng.Next(0, 1_000_000_000).ToString("D10"));
            sb.Append(RandomString(chars, 15));
            sb.Append(s_rng.NextDouble().ToString("F10").PadLeft(20, '0'));
            sb.Append(s_rng.Next(0, 100).ToString("D5"));
            sb.Append(RandomString(chars, 10));
            writer.WriteLine(sb.ToString());
        }

        return path;
    }

    private static string RandomString(string chars, int length)
    {
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[s_rng.Next(chars.Length)];
        return new string(result);
    }

    private static string Escape(string s) => s.Replace(",", "\\,");
}
