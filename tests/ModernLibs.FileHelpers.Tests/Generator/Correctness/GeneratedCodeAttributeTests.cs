// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Reflection;
using System.Text.RegularExpressions;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>Both emitted classes carry <c>[GeneratedCode]</c> so analyzers and coverage tools skip them.</summary>
[Category("Generator")]
public sealed class GeneratedCodeAttributeTests
{
    private const string Source = """
        using ModernLibs.FileHelpers;
        [DelimitedRecord(",")]
        public sealed class R
        {
            public string A = "";
        }
        """;

    private static readonly Regex AttributeLine = new(
        "GeneratedCodeAttribute\\(\"ModernLibs\\.FileHelpers\\.Generator\", \"(?<v>[^\"]*)\"\\)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5));

    [Test]
    [DisplayName("Both emitted classes carry GeneratedCode with the library version")]
    public async Task Both_Emitted_Classes_Carry_GeneratedCode()
    {
        // Act
        var run = GeneratorHarness.RunGenerator(Source);
        var matches = AttributeLine.Matches(run.GeneratedText);

        // Assert: exactly one line per emitted class
        await Assert.That(matches.Count).IsEqualTo(2);
        string version = matches[0].Groups["v"].Value;
        await Assert.That(Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5))).IsTrue().Because(version);
        await Assert.That(matches[1].Groups["v"].Value).IsEqualTo(version);

        // Assert: the version is the library's
        string libVersion = typeof(FileHelperEngine<>).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        int plus = libVersion.IndexOf('+', StringComparison.Ordinal);
        await Assert.That(version).IsEqualTo(plus < 0 ? libVersion : libVersion[..plus]);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("Two Generate calls give equal text")]
    public async Task Two_Generate_Calls_Give_Equal_Text()
    {
        // Act
        string first = GeneratorHarness.RunGenerator(Source).GeneratedText;
        string second = GeneratorHarness.RunGenerator(Source).GeneratedText;

        // Assert
        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    [DisplayName("The invalid-record descriptor carries GeneratedCode on both classes")]
    public async Task Invalid_Record_Classes_Carry_GeneratedCode()
    {
        // Arrange: [FieldQuoted] on a fixed-length field is a layout error
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class F
            {
                [FieldFixedLength(5)] [FieldQuoted] public string Name = "";
                [FieldFixedLength(3)] public string Code = "";
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await Assert.That(run.GeneratedText).Contains("InvalidDescriptor : global::ModernLibs.FileHelpers.IRecordDescriptor<");
        await Assert.That(AttributeLine.Count(run.GeneratedText)).IsEqualTo(2);
    }
}
