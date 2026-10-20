// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Generated code uses the runtime converters (FileHelpers 3.x semantics) for implicit
/// and explicit conversions, grows write buffers instead of truncating, and honours
/// <c>ConverterBase.CustomNullHandling</c>.
/// </summary>
[Category("Generator")]
public sealed class ConverterIntegrationTests
{
    [Test]
    [DisplayName("Implicit bool and int fields use the 3.x converters (y/n values, invariant numbers)")]
    public async Task ImplicitFields_UseRuntimeConverters()
    {
        // Arrange: implicit bool and int fields with 3.x-only input forms
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(";")]
            public sealed class R { public bool Flag; public int Count; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("y; 1,234 ")[0];
                    return r.Flag + "|" + r.Count;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: parsed by BoolConverter / Int32Converter
        await Assert.That(output).IsEqualTo("True|1234");
    }

    [Test]
    [DisplayName("Char fields take the first character; whitespace follows the empty-field rules")]
    public async Task CharField_FirstCharacter_And_EmptyRule()
    {
        // Arrange: char and char? fields
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { public char C; public char? N; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var r = engine.ReadString("abc, ")[0];
                    string first = r.C + "|" + (r.N is null);
                    try { engine.ReadString(" ,x"); return first + "|no error"; }
                    catch (NullValueNotFoundException) { return first + "|NullValueNotFound"; }
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: 'a' from "abc", blank char? is null, blank char raises NullValueNotFound
        await Assert.That(output).IsEqualTo("a|True|NullValueNotFound");
    }

    [Test]
    [DisplayName("Write buffers grow: Guid B format, custom bool strings and long custom output are written in full")]
    public async Task WriteBuffers_GrowInsteadOfTruncating()
    {
        // Arrange: converters whose output exceeds the historic fixed scratch sizes
        const string source = """
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Long : ConverterBase
            {
                public override object? StringToField(string text) => text.Length;
                public override string? FieldToString(object? field) => new string('x', 300);
            }
            [DelimitedRecord("|")]
            public sealed class R
            {
                [FieldConverter(ConverterKind.Guid, "B")] public System.Guid G;
                [FieldConverter(ConverterKind.Boolean, "Affirmative", "Negative")] public bool B;
                [FieldConverter(typeof(Long))] public int L;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new R { G = System.Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), B = true, L = 1 };
                    string line = new FileHelperEngine<R>().WriteString(new[] { r }).TrimEnd();
                    var parts = line.Split('|');
                    return parts[0] + "|" + parts[1] + "|" + parts[2].Length;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: nothing truncated or blank
        await Assert.That(output).IsEqualTo("{3f2504e0-4f89-11d3-9a0c-0305e82c3301}|Affirmative|300");
    }

    [Test]
    [DisplayName("A ConverterBase with CustomNullHandling receives empty input; without it the null-value rules apply")]
    public async Task CustomNullHandling_DecidesWhoHandlesEmptyInput()
    {
        // Arrange: two converters, one opting into custom null handling
        const string source = """
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Custom : ConverterBase
            {
                protected override bool CustomNullHandling => true;
                public override object? StringToField(string text) => text.Trim().Length == 0 ? -1 : int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            }
            public sealed class Plain : ConverterBase
            {
                public override object? StringToField(string text) => int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            }
            [DelimitedRecord(",")]
            public sealed class R
            {
                [FieldConverter(typeof(Custom))] public int A;
                [FieldConverter(typeof(Plain))] [FieldNullValue(7)] public int B;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString(" , ")[0];
                    return r.A + "|" + r.B;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: Custom handled the blank itself; Plain was bypassed and the null value used
        await Assert.That(output).IsEqualTo("-1|7");
    }

    [Test]
    [DisplayName("ConverterKind.DateMultiFormat without formats reports MLFH021")]
    public async Task DateMultiFormat_WithoutFormats_ReportsDiagnostic()
    {
        // Arrange: DateMultiFormat with no format arguments
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldConverter(ConverterKind.DateMultiFormat)] public System.DateTime D; }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH021 reported, nothing generated, compilation clean
        await Assert.That(run.Diagnostics("MLFH021").Count).IsEqualTo(1);
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG7: a T? field with an ISpanConverter<T> reads an empty value as null and round-trips it")]
    public async Task NullableField_WithUnderlyingSpanConverter_ReadsEmptyAsNull()
    {
        // Arrange: Money? with a converter for Money, and the MIGRATION example on DateOnly?
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public readonly record struct Money(decimal Amount);
            public sealed class MoneyConv : ISpanConverter<Money>
            {
                public bool TryParse(ReadOnlySpan<char> span, out Money value)
                {
                    bool ok = decimal.TryParse(span, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal d);
                    value = new Money(d);
                    return ok;
                }
                public bool TryFormat(Money value, Span<char> destination, out int charsWritten) =>
                    value.Amount.TryFormat(destination, out charsWritten, default, System.Globalization.CultureInfo.InvariantCulture);
            }
            public sealed class IsoDateOnlyConverter : ISpanConverter<DateOnly>
            {
                private static readonly DateOnlyConverter Inner = new("yyyy-MM-dd");
                public bool TryParse(ReadOnlySpan<char> span, out DateOnly value) => Inner.TryParse(span, out value);
                public bool TryFormat(DateOnly value, Span<char> destination, out int charsWritten) => Inner.TryFormat(value, destination, out charsWritten);
            }
            [DelimitedRecord(",")]
            public sealed class R
            {
                public int A;
                [FieldConverter(typeof(MoneyConv))] public Money? M;
                [FieldConverter(typeof(IsoDateOnlyConverter))] public DateOnly? D;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    string written = engine.WriteString(new[] { new R { A = 1 }, new R { A = 2, M = new Money(3.5m), D = new DateOnly(2024, 1, 2) } }).Replace("\r\n", ";");
                    var back = engine.ReadString(written.Replace(";", "\r\n"));
                    return written + "|" + (back[0].M is null) + (back[0].D is null) + "|" + back[1].M!.Value.Amount + " " + back[1].D;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("1,,;2,3.5,2024-01-02;|TrueTrue|3.5 " + new DateOnly(2024, 1, 2));
    }
}
