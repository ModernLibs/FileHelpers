// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// 3.x <c>[FieldConverter(typeof(MyConverter), args...)]</c>: the arguments go to a matching public constructor
/// (created with <c>new</c>, no reflection); without one the generator reports MLFH022.
/// </summary>
[Category("Generator")]
public sealed class CustomConverterArgsTests
{
    [Test]
    [DisplayName("Converter arguments are passed to a matching constructor (ConverterBase, ISpanConverter<T>, params string[])")]
    public async Task Arguments_ArePassedToConstructor()
    {
        // Arrange: three converter shapes taking string arguments
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class PrefixConverter : ConverterBase
            {
                private readonly string _prefix;
                public PrefixConverter(string prefix) => _prefix = prefix;
                public override object? StringToField(string text) => _prefix + text;
            }
            public sealed class ScaleConverter : ISpanConverter<int>
            {
                private readonly int _factor;
                public ScaleConverter(string factor, string unused) => _factor = int.Parse(factor);
                public bool TryParse(ReadOnlySpan<char> span, out int value) { bool ok = int.TryParse(span, out value); value *= _factor; return ok; }
                public bool TryFormat(int value, Span<char> destination, out int charsWritten) => (value / _factor).TryFormat(destination, out charsWritten);
            }
            public sealed class JoinConverter : ConverterBase
            {
                private readonly string _suffix;
                public JoinConverter(params string[] parts) => _suffix = string.Join("+", parts);
                public override object? StringToField(string text) => text + "|" + _suffix;
            }
            [DelimitedRecord(",")]
            public sealed class R
            {
                [FieldConverter(typeof(PrefixConverter), "X-")] public string A = "";
                [FieldConverter(typeof(ScaleConverter), "10", "n/a")] public int B;
                [FieldConverter(typeof(JoinConverter), "p", "q", "r")] public string C = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("abc,4,z")[0];
                    return r.A + "|" + r.B + "|" + r.C;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("X-abc|40|z|p+q+r");
    }

    [Test]
    [DisplayName("MLFH022: converter arguments without a matching public constructor")]
    public async Task NoMatchingConstructor_ReportsMLFH022()
    {
        // Arrange: converter with only a parameterless constructor, used with an argument
        const string source = """
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Plain : ConverterBase
            {
                public override object? StringToField(string text) => text;
            }
            [DelimitedRecord(",")]
            public sealed class R { [FieldConverter(typeof(Plain), "arg")] public string A = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: error reported at the field, no MLFH006/MLFH013 noise
        var diagnostics = run.Diagnostics("MLFH022");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("1 argument");
        await Assert.That(run.Diagnostics("MLFH006").Count).IsEqualTo(0);
        await Assert.That(run.Diagnostics("MLFH013").Count).IsEqualTo(0);
    }
}
