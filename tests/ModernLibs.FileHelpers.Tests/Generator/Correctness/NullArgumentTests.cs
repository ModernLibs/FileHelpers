// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// VG2: a <c>null</c> passed for a <c>params</c> array of a FileHelpers attribute must not crash the
/// generator (which would drop every descriptor with CS8785).
/// </summary>
[Category("Generator")]
public sealed class NullArgumentTests
{
    private static async Task AssertGenerated(GeneratorHarness.Run run, string descriptor)
    {
        foreach (var result in run.Result.Results)
            await Assert.That(result.Exception).IsNull();
        await Assert.That(run.Diagnostics("CS8785").Count).IsEqualTo(0);
        await Assert.That(run.GeneratedText).Contains(descriptor).Because(string.Join("; ", run.GeneratorDiagnostics));
        await Assert.That(run.GeneratedText).Contains("__FineDescriptor");
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG2: [FieldConverter(ConverterKind.Date, null)] is treated as no arguments")]
    public async Task ConverterKind_NullArguments_Generates()
    {
        // Arrange: null params array next to a valid record
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class D { [FieldConverter(ConverterKind.Date, null)] public DateTime When; }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await AssertGenerated(run, "__DDescriptor").ConfigureAwait(false);
    }

    [Test]
    [DisplayName("VG2: [FieldConverter(typeof(X), null)] is treated as no arguments")]
    public async Task ConverterType_NullArguments_Generates()
    {
        // Arrange: custom converter with a null params array
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Up : ConverterBase
            {
                public override object? StringToField(string text) => text.ToUpperInvariant();
            }
            [DelimitedRecord(",")]
            public sealed class C { [FieldConverter(typeof(Up), null)] public string Name = ""; }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await AssertGenerated(run, "__CDescriptor").ConfigureAwait(false);
    }

    [Test]
    [DisplayName("VG2: [FieldTrim(TrimMode.Both, null)] trims the default whitespace")]
    public async Task FieldTrim_NullChars_Generates()
    {
        // Arrange: null trim characters
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class T { [FieldTrim(TrimMode.Both, null)] public string Name = ""; }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            public static class Probe
            {
                public static string Run() => "[" + new FileHelperEngine<T>().ReadString("  x  ")[0].Name + "]";
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert
        await AssertGenerated(run, "__TDescriptor").ConfigureAwait(false);
        await Assert.That(output).IsEqualTo("[x]");
    }
}
