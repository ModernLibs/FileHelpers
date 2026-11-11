// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using ModernLibs.FileHelpers.Generator;
using ModernLibs.FileHelpers.Tests.Generator.Correctness;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// Every generator diagnostic links to its own section of <c>docs/diagnostics.md</c>, the section exists,
/// and the rule is listed as shipped in <c>AnalyzerReleases.Shipped.md</c>.
/// </summary>
[Category("Generator")]
public sealed class DiagnosticDocumentationTests
{
    private const string DocsUrl = "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#";

    private static DiagnosticDescriptor[] AllDescriptors()
    {
        Type type = typeof(FileHelpersGenerator).Assembly
            .GetType("ModernLibs.FileHelpers.Generator.Diagnostics.DiagnosticDescriptors", throwOnError: true)!;
        return type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(static f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(static f => (DiagnosticDescriptor)f.GetValue(null)!)
            .OrderBy(static d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RepoFile(string relativePath)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException("Repository file not found above the test output directory.", relativePath);
    }

    [Test]
    [DisplayName("Rules MLFH001..MLFH033 exist with consecutive ids")]
    public async Task Descriptors_AreConsecutive()
    {
        // Arrange / Act: all descriptor ids
        string[] ids = AllDescriptors().Select(static d => d.Id).ToArray();

        // Assert: MLFH001..MLFH033
        string[] expected = Enumerable.Range(1, 33).Select(static n => $"MLFH{n:000}").ToArray();
        await Assert.That(ids).IsEquivalentTo(expected);
    }

    [Test]
    [DisplayName("Every diagnostic's help link points to its docs/diagnostics.md section")]
    public async Task HelpLinks_PointToDiagnosticsDoc()
    {
        // Arrange / Act: help links that do not match the per-rule anchor
        string[] wrong = AllDescriptors()
            .Where(static d => !string.Equals(d.HelpLinkUri, DocsUrl + d.Id.ToLowerInvariant(), StringComparison.Ordinal))
            .Select(static d => d.Id + " -> " + d.HelpLinkUri)
            .ToArray();

        // Assert: none
        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    [DisplayName("docs/diagnostics.md has a section for every rule")]
    public async Task DiagnosticsDoc_HasSectionPerRule()
    {
        // Arrange: the documentation page
        string doc = RepoFile(Path.Combine("docs", "diagnostics.md"));

        // Act: rules without a heading that is exactly "## MLFHnnn" (GitHub generates the anchor #mlfhnnn)
        string[] missing = AllDescriptors()
            .Where(d => !Regex.IsMatch(doc, "^## " + d.Id + "\\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            .Select(static d => d.Id)
            .ToArray();

        // Assert: none
        await Assert.That(missing).IsEmpty();
    }

    [Test]
    [DisplayName("Each documented example marked '// MLFHnnn' produces that diagnostic")]
    public async Task DiagnosticsDoc_ExamplesProduceTheirDiagnostic()
    {
        // Arrange: C# code blocks of docs/diagnostics.md that carry a "// MLFHnnn" marker
        string doc = RepoFile(Path.Combine("docs", "diagnostics.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        const RegexOptions options = RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant;
        var blocks = Regex.Matches(doc, "```csharp\n(?<code>.*?)```", options | RegexOptions.Singleline, TimeSpan.FromSeconds(1))
            .Select(static m => m.Groups["code"].Value)
            .Select(static code => (Id: Regex.Match(code, @"// (?<id>MLFH\d{3})", options, TimeSpan.FromSeconds(1)).Groups["id"].Value, Code: code))
            .Where(static b => b.Id.Length > 0)
            .ToArray();
        const string usings = "using System; using System.Globalization; using ModernLibs.FileHelpers; using ModernLibs.FileHelpers.Converters;\n";

        // Act: run the generator over each example
        var wrong = new List<string>();
        foreach (var (id, code) in blocks)
        {
            var run = GeneratorHarness.RunGenerator(usings + code);
            if (run.Diagnostics(id).Count == 0)
                wrong.Add(id + ": got [" + string.Join(", ", run.GeneratorDiagnostics.Select(static d => d.Id)) + "]");
        }

        // Assert: every example except MLFH020 (internal failure) is checked and reports its rule
        await Assert.That(blocks.Length).IsEqualTo(31);
        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    [DisplayName("Rules MLFH001..MLFH029 are shipped in release 4.8.0; every later rule is in the unshipped list")]
    public async Task AnalyzerReleases_AreShipped()
    {
        // Arrange: release tracking files
        string shipped = RepoFile(Path.Combine("src", "ModernLibs.FileHelpers.Generator", "Diagnostics", "AnalyzerReleases.Shipped.md"));
        string unshipped = RepoFile(Path.Combine("src", "ModernLibs.FileHelpers.Generator", "Diagnostics", "AnalyzerReleases.Unshipped.md"));

        // Act: rules missing from the table they belong to (MLFH001..MLFH029 shipped in 4.8.0, the rest unshipped)
        var descriptors = AllDescriptors();
        string[] missingShipped = descriptors
            .Where(d => string.CompareOrdinal(d.Id, "MLFH029") <= 0 && !shipped.Contains("\n" + d.Id + " | ", StringComparison.Ordinal))
            .Select(static d => d.Id)
            .ToArray();
        // The unshipped rows as "id | severity", compared with the descriptors above 029 (a stray row, a missing
        // row or a wrong severity fails).
        string[] unshippedRows = Regex.Matches(unshipped, @"^(?<id>MLFH\d{3}) \| [^|]+ \| (?<severity>[A-Za-z]+) \|", RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(static m => m.Groups["id"].Value + " " + m.Groups["severity"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedUnshipped = descriptors
            .Where(static d => string.CompareOrdinal(d.Id, "MLFH029") > 0)
            .Select(static d => d.Id + " " + d.DefaultSeverity)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        await Assert.That(shipped).Contains("## Release 4.8.0");
        await Assert.That(missingShipped).IsEmpty();
        await Assert.That(unshippedRows).IsEquivalentTo(expectedUnshipped);
        await Assert.That(shipped).DoesNotContain("MLFH030");
    }
}
