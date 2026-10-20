// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Generated code must compile without warnings for every supported record shape, so consumers that
/// build with <c>TreatWarningsAsErrors</c> (and every warning wave) are never broken by it.
/// </summary>
[Category("Generator")]
public sealed class GeneratedCodeWarningTests
{
    [Test]
    [DisplayName("A first [FieldInNewLine] field with AlwaysQuoted generates code without unreachable-code warnings")]
    public async Task InNewLineAlwaysQuoted_NoUnreachableCode()
    {
        // Arrange: a required quoted field that starts the second physical line
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                public string A = "";
                [FieldInNewLine, FieldQuoted(QuoteMode.AlwaysQuoted)] public string B = "";
                public string C = "";
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: the descriptor is generated and has no warnings (CS0162 before the fix)
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue().Because(string.Join("\n", run.GeneratedWarnings));
    }

    [Test]
    [DisplayName("A required quoted field at the start of a line still rejects an unquoted value")]
    public async Task LineStartAlwaysQuoted_RejectsUnquotedValue()
    {
        // Arrange: AlwaysQuoted first field and AlwaysQuoted [FieldInNewLine] field
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                [FieldQuoted(QuoteMode.AlwaysQuoted)] public string A = "";
                [FieldInNewLine, FieldQuoted(QuoteMode.AlwaysQuoted)] public string B = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var ok = engine.ReadString("\"a\",\r\n\"b\"\r\n")[0];
                    string first, second;
                    try { engine.ReadString("a,\r\n\"b\"\r\n"); first = "read"; } catch (BadUsageException) { first = "bad"; }
                    try { engine.ReadString("\"a\",\r\nb\r\n"); second = "read"; } catch (BadUsageException) { second = "bad"; }
                    return ok.A + ok.B + "|" + first + "|" + second;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: quoted values are read, unquoted ones raise BadUsageException on both lines
        await Assert.That(output).IsEqualTo("ab|bad|bad");
    }

    [Test]
    [DisplayName("Generated code has no warnings for a broad corpus of record shapes (C# 11 and latest)")]
    public async Task RecordShapeCorpus_GeneratesNoWarnings()
    {
        // Arrange: one compilation with every shape in its own namespace
        string source = BuildCorpus(out int recordCount);
        var failures = new StringBuilder();

        foreach (var languageVersion in new[] { LanguageVersion.CSharp11, LanguageVersion.Preview })
        {
            // Act
            var run = GeneratorHarness.RunGenerator(source, languageVersion: languageVersion);

            // Assert (collected): every record generated, no errors, no warnings in generated files
            if (run.Result.GeneratedTrees.Length != recordCount)
                failures.Append(CultureInfo.InvariantCulture, $"[{languageVersion}] generated {run.Result.GeneratedTrees.Length} of {recordCount} descriptors: {string.Join("; ", run.GeneratorDiagnostics)}\n");
            if (!run.CompileErrors.IsEmpty)
                failures.Append(CultureInfo.InvariantCulture, $"[{languageVersion}] errors:\n{run.ErrorText}\n");
            foreach (var warning in run.GeneratedWarnings)
                failures.Append(CultureInfo.InvariantCulture, $"[{languageVersion}] {warning}\n");
        }

        await Assert.That(failures.ToString()).IsEmpty();
    }

    private static readonly string[] Shapes =
        [
            // Every built-in field type, plain and nullable.
            """
            [DelimitedRecord(",")] public class AllTypes
            {
                public string S = ""; public bool B; public char C; public byte U8; public sbyte I8; public short I16; public ushort U16;
                public int I32; public uint U32; public long I64; public ulong U64; public decimal M; public double D; public float F;
                public DateTime Dt; public DateOnly Do; public TimeOnly To; public DateTimeOffset Dto; public TimeSpan Ts; public Guid G; public Color E;
                public bool? NB; public char? NC; public int? NI; public decimal? NM; public double? ND; public DateTime? NDt; public DateOnly? NDo;
                public TimeOnly? NTo; public DateTimeOffset? NDto; public TimeSpan? NTs; public Guid? NG; public Color? NE; public string? NS;
            }
            public enum Color { Red, Green }
            """,
            """
            [FixedLengthRecord(FixedMode.AllowVariableLength)] public struct FixedTypes
            {
                [FieldFixedLength(3)] public string S; [FieldFixedLength(5)] public int I; [FieldFixedLength(8), FieldAlign(AlignMode.Right, '0')] public decimal M;
                [FieldFixedLength(10), FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime D; [FieldFixedLength(4, OverflowMode = OverflowMode.Error)] public string O;
                [FieldFixedLength(4, OverflowMode = OverflowMode.DiscardEnd)] public string O2; [FieldFixedLength(6), FieldAlign(AlignMode.Center)] public double R;
                [FieldFixedLength(2), FieldOptional] public int? Opt; [FieldFixedLength(3), FieldOptional, FieldTrim(TrimMode.Both)] public string Last;
            }
            [FixedLengthRecord(FixedMode.ExactLength)] public class FixedExact { [FieldFixedLength(2)] public string A = ""; [FieldFixedLength(2), FieldInNewLine] public string B = ""; [FieldFixedLength(2), FieldValueDiscarded] public string C = ""; }
            [FixedLengthRecord(FixedMode.AllowMoreChars)] public class FixedMore { [FieldFixedLength(2), FieldInNewLine] public int A; [FieldFixedLength(2)] public int B; }
            [FixedLengthRecord(FixedMode.AllowLessChars)] public class FixedLess { [FieldFixedLength(2)] public int A; [FieldFixedLength(2), FieldNullValue(7)] public int B; }
            """,
            // Per-field and multi-character delimiters, trimming, captions, not-empty, discarded, hidden.
            """
            [DelimitedRecord("||")] public class Delims
            {
                [FieldCaption("First")] public string A = ""; [FieldDelimiter(";")] public int B; [FieldDelimiter("<>"), FieldTrim(TrimMode.Both)] public string C = "";
                [FieldTrim(TrimMode.Left, ' ', '-')] public string D = ""; [FieldTrim(TrimMode.Right)] public string E = ""; [FieldNotEmpty] public string F = "";
                [FieldValueDiscarded] public int G; [FieldHidden] public int H; [FieldNotInFile] public string I = ""; [FieldOptional] public string? J;
            }
            [DelimitedRecord("\t")] public class Tabs { [FieldFixedLength(4), FieldAlign(AlignMode.Left, '.')] public string A = ""; public int B; }
            """,
            // Converters: kinds with arguments, culture, custom ConverterBase, ISpanConverter (direct and nullable underlying).
            """
            [DelimitedRecord(";")] public class Converted
            {
                [FieldConverter(ConverterKind.Date, "dd.MM.yyyy", "de-DE")] public DateTime D;
                [FieldConverter(ConverterKind.DateMultiFormat, "yyyy-MM-dd", "dd/MM/yyyy")] public DateTime? DM;
                [FieldConverter(ConverterKind.Decimal, ",")] public decimal M; [FieldConverter(ConverterKind.Double, ".")] public double Db;
                [FieldConverter(ConverterKind.PercentDouble)] public double P; [FieldConverter(ConverterKind.Int32, ",")] public int I;
                [FieldConverter(ConverterKind.Boolean, "Y", "N")] public bool B; [FieldConverter(ConverterKind.Guid, "N")] public Guid G;
                [FieldConverter(ConverterKind.Enum)] public Shade E; [FieldConverter(ConverterKind.Char)] public char C;
                [FieldConverter(ConverterKind.Int64, CultureName = "fr-FR")] public long L;
                [FieldConverter(typeof(Upper))] public string U = ""; [FieldConverter(typeof(Prefix), "x")] public string Pr = "";
                [FieldConverter(typeof(MoneyConverter))] public Money Mo; [FieldConverter(typeof(MoneyConverter))] public Money? NMo;
                [FieldNullValue(-1)] public int NV; [FieldNullValue(1.5)] public decimal NVM; [FieldNullValue(Shade.Dark)] public Shade NVE; [FieldNullValue("n/a")] public string NVS = "";
            }
            public enum Shade { Light, Dark }
            public sealed class Upper : ConverterBase { public override object? StringToField(string from) => from.ToUpperInvariant(); }
            public sealed class Prefix : ConverterBase { private readonly string _p; public Prefix(string p) { _p = p; } public override object? StringToField(string from) => _p + from; }
            public struct Money { public decimal V; }
            public sealed class MoneyConverter : ISpanConverter<Money>
            {
                public bool TryParse(ReadOnlySpan<char> s, out Money v) { v = new Money { V = decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture) }; return true; }
                public bool TryFormat(Money v, Span<char> d, out int w) => v.V.TryFormat(d, out w, default, System.Globalization.CultureInfo.InvariantCulture);
            }
            """,
            // Construction strategies.
            """
            [DelimitedRecord(",")] public class CtorBound { public CtorBound(int a, string b) { A = a; B = b; } public int A { get; } public string B { get; } }
            [DelimitedRecord(",")] public record Positional(int A, string B);
            [DelimitedRecord(",")] public record struct PositionalStruct(int A, string B);
            [DelimitedRecord(",")] public readonly struct ReadOnlyStruct { public ReadOnlyStruct(int a, string b) { A = a; B = b; } public int A { get; } public string B { get; } }
            [DelimitedRecord(",")] public class InitOnly { public int A { get; init; } public string B { get; init; } = ""; }
            [DelimitedRecord(",")] public class Required { public required int A { get; set; } public required string B { get; init; } public string C = ""; [FieldHidden] public required string H { get; set; } }
            [DelimitedRecord(",")] public class SetsRequired { [SetsRequiredMembers] public SetsRequired() { B = ""; } public int A; public required string B { get; set; } }
            [DelimitedRecord(",")] public class Uninitialized : INotifyRead<Uninitialized> { private Uninitialized() { } public Uninitialized(int a) { A = a; } public int A { get; init; } public void BeforeRead(BeforeReadEventArgs<Uninitialized> e) { } public void AfterRead(AfterReadEventArgs<Uninitialized> e) { } }
            [DelimitedRecord(",")] public class Notify : INotifyRead<Notify>, INotifyWrite<Notify> { public int A; public void BeforeRead(BeforeReadEventArgs<Notify> e) { } public void AfterRead(AfterReadEventArgs<Notify> e) { } public void BeforeWrite(BeforeWriteEventArgs<Notify> e) { } public void AfterWrite(AfterWriteEventArgs<Notify> e) { } }
            [DelimitedRecord(",")] public record struct StructRequired { public required int A { get; set; } public string B { get; set; } }
            """,
            // Record-level options.
            """
            [DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)] [IgnoreFirst(2)] [IgnoreLast] [IgnoreEmptyLines(true)] [IgnoreCommentedLines('#', false)]
            [ConditionalRecord(RecordCondition.ExcludeIfRegex, "^x")] public class Options1 { [FieldQuoted('\'')] public string A = ""; public int B; }
            [DelimitedRecord(",")] [ConditionalRecord(RecordCondition.IncludeIfBegins, "a")] public class Options2 { public string A = ""; }
            [DelimitedRecord(",")] [ConditionalRecord(RecordCondition.IncludeIfContains, "a")] public class Options3 { public string A = ""; }
            [DelimitedRecord(",")] [ConditionalRecord(RecordCondition.ExcludeIfEnds, "a")] public class Options4 { public string A = ""; }
            [DelimitedRecord(",")] [ConditionalRecord(RecordCondition.IncludeIfRegex, "a+")] [IgnoreEmptyLines] public class Options5 { public string A = ""; }
            """,
            // Names, nesting, inheritance and obsolete members.
            """
            public static class @class { [DelimitedRecord(",")] public class @event { public int @int; public string @string = ""; } }
            public class Outer { [FixedLengthRecord] internal sealed class Inner { [FieldFixedLength(1)] internal string A = ""; } }
            public class Base0 { public int B0; } [IgnoreInheritedClass] public class Base1 : Base0 { public int B1; } [DelimitedRecord(",")] public class Derived : Base1 { public int C1; }
            [DelimitedRecord(",")] public class Obsoletes { [Obsolete] public int A; [Obsolete("x")] public string B { get; set; } = ""; }
            [Obsolete] [DelimitedRecord(",")] public class ObsoleteRecord { public int A; }
            """,
        ];

    private static string BuildCorpus(out int recordCount)
    {
        var shapes = new List<string>(Shapes);
        foreach (string mode in (string[])["AlwaysQuoted", "OptionalForRead", "OptionalForWrite", "OptionalForBoth"])
            shapes.Add(QuotedShapes(mode));

        var source = new StringBuilder();
        source.AppendLine("using System;");
        source.AppendLine("using System.Diagnostics.CodeAnalysis;");
        source.AppendLine("using ModernLibs.FileHelpers;");
        source.AppendLine("using ModernLibs.FileHelpers.Converters;");
        for (int i = 0; i < shapes.Count; i++)
        {
            source.AppendLine(CultureInfo.InvariantCulture, $"namespace Shape{i}");
            source.AppendLine("{");
            source.AppendLine(shapes[i]);
            source.AppendLine("}");
        }

        string text = source.ToString();
        recordCount = CountOccurrences(text, "[DelimitedRecord(") + CountOccurrences(text, "[FixedLengthRecord");
        return text;
    }

    /// <summary>[FieldQuoted] in one QuoteMode, at the start, middle and end of a line, required and optional.</summary>
    private static string QuotedShapes(string mode) => $$"""
                [DelimitedRecord(",")] public class Q{{mode}}
                {
                    [FieldQuoted(QuoteMode.{{mode}})] public string First = "";
                    [FieldQuoted('"', QuoteMode.{{mode}}, MultilineMode.NotAllow), FieldTrim(TrimMode.Both)] public string Middle = "";
                    [FieldInNewLine, FieldQuoted(QuoteMode.{{mode}})] public string LineStart = "";
                    [FieldQuoted(QuoteMode.{{mode}}), FieldTrim(TrimMode.Left)] public string LineEnd = "";
                    [FieldInNewLine, FieldQuoted(QuoteMode.{{mode}}), FieldTrim(TrimMode.Right, ' ')] public string Alone = "";
                    [FieldInNewLine, FieldQuoted(QuoteMode.{{mode}}), FieldOptional] public string? OptionalStart;
                    [FieldQuoted(QuoteMode.{{mode}}, MultilineMode.AllowForWrite), FieldOptional] public string? OptionalEnd;
                }
                [DelimitedRecord(";")] public class QSingle{{mode}} { [FieldQuoted(QuoteMode.{{mode}})] public string Only = ""; }
                [DelimitedRecord(";")] public class QOptional{{mode}} { public int A; [FieldQuoted(QuoteMode.{{mode}}), FieldOptional] public string? B; [FieldQuoted(QuoteMode.{{mode}}), FieldOptional] public string? C; }
                [DelimitedRecord("|")] public class QDelim{{mode}} { [FieldQuoted(QuoteMode.{{mode}})] public string A = ""; [FieldDelimiter("::"), FieldQuoted(QuoteMode.{{mode}})] public string B = ""; public int C; }
                """;

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
