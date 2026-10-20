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
    [DisplayName("Rules MLFH001..MLFH029 exist with consecutive ids")]
    public async Task Descriptors_AreConsecutive()
    {
        // Arrange / Act: all descriptor ids
        string[] ids = AllDescriptors().Select(static d => d.Id).ToArray();

        // Assert: MLFH001..MLFH029
        string[] expected = Enumerable.Range(1, 29).Select(static n => $"MLFH{n:000}").ToArray();
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
        await Assert.That(blocks.Length).IsEqualTo(27);
        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    [DisplayName("Every rule is listed as shipped in release 4.8.0 and the unshipped list is empty")]
    public async Task AnalyzerReleases_AreShipped()
    {
        // Arrange: release tracking files
        string shipped = RepoFile(Path.Combine("src", "ModernLibs.FileHelpers.Generator", "Diagnostics", "AnalyzerReleases.Shipped.md"));
        string unshipped = RepoFile(Path.Combine("src", "ModernLibs.FileHelpers.Generator", "Diagnostics", "AnalyzerReleases.Unshipped.md"));

        // Act: rules missing from the shipped table
        string[] missing = AllDescriptors()
            .Where(d => !shipped.Contains("\n" + d.Id + " | ", StringComparison.Ordinal))
            .Select(static d => d.Id)
            .ToArray();

        // Assert
        await Assert.That(shipped).Contains("## Release 4.8.0");
        await Assert.That(missing).IsEmpty();
        await Assert.That(unshipped).DoesNotContain("MLFH");
    }
}
