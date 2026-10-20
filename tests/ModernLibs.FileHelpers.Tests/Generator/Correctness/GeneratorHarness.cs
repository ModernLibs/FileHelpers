using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ModernLibs.FileHelpers.Generator;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// In-process harness for generator correctness tests.
/// Compiles a consumer source together with the generator output against the full .NET
/// reference set, WITHOUT implicit usings (generated code must not rely on
/// <c>ImplicitUsings</c>), and can load the result to execute engine scenarios.
/// </summary>
internal static class GeneratorHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyReferences = new(LoadReferences);

    /// <summary>The .NET reference set plus ModernLibs.FileHelpers.</summary>
    internal static ImmutableArray<MetadataReference> References => LazyReferences.Value;

    /// <summary>Result of a generator run plus the compilation of its output.</summary>
    internal sealed record Run(
        GeneratorDriverRunResult Result,
        Compilation Output,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<Diagnostic> CompileErrors,
        ImmutableArray<Diagnostic> GeneratedWarnings)
    {
        /// <summary>All generated source text concatenated (for content assertions).</summary>
        public string GeneratedText =>
            string.Join("\n// ----\n", Result.GeneratedTrees.Select(static t => t.GetText().ToString()));

        /// <summary>Generator diagnostics with the given id.</summary>
        public IReadOnlyList<Diagnostic> Diagnostics(string id) =>
            GeneratorDiagnostics.Where(d => string.Equals(d.Id, id, StringComparison.Ordinal)).ToList();

        /// <summary>Compile errors formatted for assertion messages.</summary>
        public string ErrorText =>
            string.Join("\n", CompileErrors.Select(static d => d.ToString()))
            + string.Join("\n", GeneratorDiagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).Select(static d => d.ToString()));
    }

    /// <summary>
    /// Generated descriptors that work, i.e. excluding the throwing descriptor registered for a record with
    /// FileHelpers errors.
    /// </summary>
    internal static int WorkingDescriptorCount(GeneratorDriverRunResult result) =>
        result.GeneratedTrees.Count(static t => !t.GetText().ToString().Contains("InvalidDescriptor : global::ModernLibs.FileHelpers.IRecordDescriptor<", StringComparison.Ordinal));

    /// <summary>
    /// Runs the generator over <paramref name="source"/> and compiles the output.
    /// <paramref name="globalOptions"/> feeds <c>AnalyzerConfigOptions.GlobalOptions</c> (build properties).
    /// </summary>
    internal static Run RunGenerator(string source, IReadOnlyDictionary<string, string>? globalOptions = null, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var compilation = CSharpCompilation.Create(
            "GenHarness_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "Consumer.cs")],
            References,
            // Highest warning level (every warning wave), so GeneratedWarnings sees what a consumer with
            // TreatWarningsAsErrors and the latest analysis level would see.
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, warningLevel: 9999));

        var optionsProvider = globalOptions is null ? null : new DictionaryOptionsProvider(globalOptions);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new FileHelpersGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var driverDiagnostics);
        var result = driver.GetRunResult();

        // VG1: validation diagnostics come from the analyzer, which runs on the final compilation.
        var generatorDiagnostics = driverDiagnostics.AddRange(AnalyzerDiagnostics(output, optionsProvider));

        var all = output.GetDiagnostics();
        var errors = all.Where(static d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        var generatedPaths = result.GeneratedTrees.Select(static t => t.FilePath).ToHashSet(StringComparer.Ordinal);
        var generatedWarnings = all
            .Where(d => d.Severity == DiagnosticSeverity.Warning
                        && d.Location.SourceTree is not null
                        && generatedPaths.Contains(d.Location.SourceTree.FilePath))
            .ToImmutableArray();
        return new Run(result, output, generatorDiagnostics, errors, generatedWarnings);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> with the generator, loads it into a collectible context and
    /// invokes <c>public static string Probe.Run()</c>. Throws when the compilation has errors.
    /// </summary>
    internal static string Execute(string source)
    {
        var run = RunGenerator(source);
        if (!run.CompileErrors.IsEmpty)
            throw new InvalidOperationException("Compilation failed:\n" + run.ErrorText + "\n" + run.GeneratedText);

        using var stream = new MemoryStream();
        var emit = run.Output.Emit(stream);
        if (!emit.Success)
            throw new InvalidOperationException("Emit failed:\n" + string.Join("\n", emit.Diagnostics));
        stream.Position = 0;

        var context = new AssemblyLoadContext("GenHarness", isCollectible: true);
        try
        {
            Assembly assembly = context.LoadFromStream(stream);
            MethodInfo probe = assembly.GetType("Probe", throwOnError: true)!.GetMethod("Run")!;
            try
            {
                return (string)probe.Invoke(null, null)!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                return "EXCEPTION " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message;
            }
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    /// Runs <see cref="FileHelpersAnalyzer"/> over <paramref name="compilation"/> (pragma-suppressed diagnostics
    /// are excluded). An analyzer exception fails the test instead of turning into an AD0001 warning.
    /// </summary>
    internal static ImmutableArray<Diagnostic> AnalyzerDiagnostics(Compilation compilation, IReadOnlyDictionary<string, string>? globalOptions = null) =>
        AnalyzerDiagnostics(compilation, globalOptions is null ? null : new DictionaryOptionsProvider(globalOptions));

    private static ImmutableArray<Diagnostic> AnalyzerDiagnostics(Compilation compilation, AnalyzerConfigOptionsProvider? optionsProvider)
    {
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty,
            optionsProvider ?? new DictionaryOptionsProvider(new Dictionary<string, string>(StringComparer.Ordinal)));
        Exception? failure = null;
        var withAnalyzers = compilation.WithAnalyzers(
            [new FileHelpersAnalyzer()],
            new CompilationWithAnalyzersOptions(options, (ex, _, _) => failure ??= ex, concurrentAnalysis: false, logAnalyzerExecutionTime: false));
        var diagnostics = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        if (failure is not null)
            throw new InvalidOperationException("FileHelpersAnalyzer threw.", failure);
        return diagnostics;
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in tpa.Split(Path.PathSeparator))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("System.", StringComparison.Ordinal)
                || string.Equals(name, "System", StringComparison.Ordinal)
                || string.Equals(name, "netstandard", StringComparison.Ordinal)
                || string.Equals(name, "mscorlib", StringComparison.Ordinal))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }
        builder.Add(MetadataReference.CreateFromFile(typeof(DelimitedRecordAttribute).Assembly.Location));
        return builder.ToImmutable();
    }

    private sealed class DictionaryOptionsProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly DictionaryOptions _global = new(values);
        private static readonly DictionaryOptions Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class DictionaryOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        {
            if (values.TryGetValue(key, out string? v))
            {
                value = v;
                return true;
            }
            value = null;
            return false;
        }
    }
}
