// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// A record with a FileHelpers error gets no working descriptor. Hiding the error must therefore never
/// turn into a silent runtime failure: the blocking errors are not configurable (no <c>#pragma</c>,
/// <c>.editorconfig</c> severity or <c>NoWarn</c>), and when the error is hidden anyway
/// (<c>[SuppressMessage]</c>, analyzers turned off) the registered descriptor throws a
/// <see cref="BadUsageException"/> that names the record and the rule.
/// </summary>
[Category("Generator")]
public sealed class SuppressionTests
{
    private const string MixedOrderRecord = """
        using ModernLibs.FileHelpers;
        namespace N;
        [DelimitedRecord(",")]
        public sealed class R
        {
            [FieldOrder(1)] public int A;
        #pragma warning disable MLFH024
            public int B;
        #pragma warning restore MLFH024
        }
        """;

    [Test]
    [DisplayName("Every error except MLFH001 and MLFH020 is NotConfigurable; warnings stay configurable")]
    public async Task BlockingErrors_AreNotConfigurable()
    {
        // Arrange / Act: descriptors by configurability
        var descriptors = typeof(FileHelpersGenerator).Assembly
            .GetType("ModernLibs.FileHelpers.Generator.Diagnostics.DiagnosticDescriptors", throwOnError: true)!
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(static f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(static f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToList();
        static bool NotConfigurable(DiagnosticDescriptor d) => d.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable, StringComparer.Ordinal);
        var wrong = descriptors
            .Where(static d => NotConfigurable(d) != (d.DefaultSeverity == DiagnosticSeverity.Error && d.Id is not ("MLFH001" or "MLFH020")))
            .Select(static d => d.Id)
            .ToList();

        // Assert
        await Assert.That(descriptors.Count).IsGreaterThan(20);
        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    [DisplayName("#pragma warning disable, severity configuration and /nowarn cannot hide a blocking error")]
    public async Task PragmaAndSeverityOptions_DoNotHideBlockingError()
    {
        // Arrange: the record wraps the member in #pragma warning disable MLFH024
        var pragmaOnly = Analyze(MixedOrderRecord, static o => o);
        var severityNone = Analyze(MixedOrderRecord, static o => o.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal) { ["MLFH024"] = ReportDiagnostic.Suppress }));
        var allSuppressed = Analyze(MixedOrderRecord, static o => o.WithGeneralDiagnosticOption(ReportDiagnostic.Suppress));

        // Assert: still an unsuppressed error every time
        foreach (var diagnostics in new[] { pragmaOnly, severityNone, allSuppressed })
        {
            var mlfh024 = diagnostics.Single(static d => string.Equals(d.Id, "MLFH024", StringComparison.Ordinal));
            await Assert.That(mlfh024.Severity).IsEqualTo(DiagnosticSeverity.Error);
            await Assert.That(mlfh024.IsSuppressed).IsFalse();
        }
    }

    [Test]
    [DisplayName("A blocking error hidden by [SuppressMessage] or disabled analyzers fails loudly at run time")]
    public async Task HiddenBlockingError_ThrowsBadUsageExceptionNamingRecordAndRule()
    {
        // Arrange: [SuppressMessage] hides MLFH024 from the analyzer; Execute runs the generator alone
        // (as with RunAnalyzers=false)
        const string source = """
            using ModernLibs.FileHelpers;
            namespace N
            {
                [DelimitedRecord(",")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("FileHelpers.Generator", "MLFH024")]
                public sealed class R
                {
                    [FieldOrder(1)] public int A;
                    public int B;
                }
            }
            public static class Probe
            {
                public static string Run() => new FileHelperEngine<N.R>().ReadString("1,2").Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert: hidden at compile time, but no silent failure at run time
        await Assert.That(run.Diagnostics("MLFH024").Count).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
        await Assert.That(output).StartsWith("EXCEPTION BadUsageException: No descriptor was generated for 'N.R'");
        await Assert.That(output).Contains("MLFH024: Field 'B' in type 'N.R' must be marked with [FieldOrder]");
    }

    [Test]
    [Arguments("MLFH004", "[FieldConverter(typeof(NotAConverter))] public int A;")]
    [Arguments("MLFH006", "[FieldConverter(typeof(NeedsArgument))] public int A;")]
    [Arguments("MLFH010", "[FieldNullValue(\"n/a\")] public int A;")]
    [DisplayName("A converter or null value error hidden by [SuppressMessage] or disabled analyzers fails loudly at run time")]
    public async Task HiddenAttributeError_ThrowsBadUsageExceptionNamingRule(string rule, string member)
    {
        // Arrange: the attribute cannot be applied, so the record must not be read without it
        string suppression = $"[System.Diagnostics.CodeAnalysis.SuppressMessage(\"FileHelpers.Generator\", \"{rule}\")]";
        string source = $$"""
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            namespace N
            {
                public sealed class NotAConverter { }
                public sealed class NeedsArgument : ConverterBase
                {
                    public NeedsArgument(string prefix) { }
                    public override object? StringToField(string text) => 42;
                }
                [DelimitedRecord(",")]
                {{suppression}}
                public sealed class R { {{member}} }
            }
            public static class Probe
            {
                public static string Run() => new FileHelperEngine<N.R>().ReadString("1\n\n").Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            """;

        // Act
        var unsuppressed = GeneratorHarness.RunGenerator(source.Replace(suppression, string.Empty, StringComparison.Ordinal));
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert: the rule is a not-configurable error, hidden here only by [SuppressMessage], and using the
        // record throws instead of ignoring the attribute
        await Assert.That(run.Diagnostics(rule).Count).IsEqualTo(0);
        var diagnostic = unsuppressed.Diagnostics(rule).Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Descriptor.CustomTags).Contains(WellKnownDiagnosticTags.NotConfigurable);
        await Assert.That(output).StartsWith("EXCEPTION BadUsageException: No descriptor was generated for 'N.R'");
        await Assert.That(output).Contains(rule + ": Field 'A' in type 'R'");
    }

    [Test]
    [DisplayName("A record without any descriptor (analyzers off, no throwing descriptor possible) gets a helpful error")]
    public async Task MissingDescriptor_MessageNamesLikelyCausesAndDocs()
    {
        // Arrange: a private nested record (MLFH017) cannot get even the throwing descriptor; Execute runs the
        // generator alone, as with RunAnalyzers=false
        const string source = """
            using ModernLibs.FileHelpers;
            public static class Probe
            {
                [DelimitedRecord(",")] private sealed class Hidden { public int A; }
                public static string Run() => new FileHelperEngine<Hidden>().ReadString("1").Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: the message names the type, the likely causes and the diagnostics reference
        await Assert.That(output).StartsWith("EXCEPTION InvalidOperationException: No IRecordDescriptor<Hidden> registered");
        await Assert.That(output).Contains("Probe+Hidden");
        await Assert.That(output).Contains("[DelimitedRecord]");
        await Assert.That(output).Contains("MLFH017");
        await Assert.That(output).Contains("MLFH026");
        await Assert.That(output).Contains("RunAnalyzers");
        await Assert.That(output).Contains("docs/diagnostics.md");
    }

    [Test]
    [DisplayName("Every descriptor member of a record with errors throws BadUsageException")]
    public async Task InvalidDescriptor_AllMembersThrow()
    {
        // Arrange: a record with MLFH013 (unsupported field type), generator only
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { public System.Uri Link = null!; }
            public static class Probe
            {
                public static string Run()
                {
                    var descriptor = FileHelperRegistry<R>.Descriptor;
                    var results = new System.Collections.Generic.List<string>();
                    void Check(System.Action action, string name)
                    {
                        try { action(); results.Add(name + ":no-throw"); }
                        catch (BadUsageException ex) when (ex.Message.Contains("MLFH013", System.StringComparison.Ordinal)) { results.Add(name + ":ok"); }
                    }
                    Check(() => descriptor.Parse(System.MemoryExtensions.AsSpan("x"), 1), "Parse");
                    Check(() => descriptor.Format(new R(), new System.Buffers.ArrayBufferWriter<char>()), "Format");
                    Check(() => _ = descriptor.Fields.Length, "Fields");
                    Check(() => _ = descriptor.Options, "Options");
                    Check(() => descriptor.CreateDefaultInstance(), "CreateDefaultInstance");
                    return string.Join(",", results);
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("Parse:ok,Format:ok,Fields:ok,Options:ok,CreateDefaultInstance:ok");
    }

    [Test]
    [DisplayName("No throwing descriptor is generated for records the generated code cannot reference")]
    public async Task UnreferenceableRecords_GetNoThrowingDescriptor()
    {
        // Arrange: generic, ref struct, private nested, static and file-local records (all with errors)
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public class G<T> { public int A; }
            [DelimitedRecord(",")] public ref struct S { public int A; }
            public class O { [DelimitedRecord(",")] private class P { public int A; } }
            [DelimitedRecord(",")] public static class St { }
            [DelimitedRecord(",")] file class F { public object A = null!; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: errors from the analyzer, but no generated code and so no compile errors in it
        await Assert.That(run.GeneratorDiagnostics.Count(static d => d.Severity == DiagnosticSeverity.Error)).IsGreaterThanOrEqualTo(4);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    /// <summary>Runs the generator and the analyzer with the given compilation options (pragmas apply).</summary>
    private static ImmutableArray<Diagnostic> Analyze(string source, Func<CSharpCompilationOptions, CSharpCompilationOptions> configure)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var options = configure(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var compilation = CSharpCompilation.Create("Suppression_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "Consumer.cs")], GeneratorHarness.References, options);
        CSharpGeneratorDriver.Create([new FileHelpersGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var withAnalyzers = output.WithAnalyzers([new FileHelpersAnalyzer()],
            new CompilationWithAnalyzersOptions(new AnalyzerOptions([]), onAnalyzerException: null, concurrentAnalysis: false,
                logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true));
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}
