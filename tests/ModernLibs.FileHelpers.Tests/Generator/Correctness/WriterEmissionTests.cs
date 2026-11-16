// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Text.RegularExpressions;
using ModernLibs.FileHelpers.Tests.Generator.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// The emitted text of the write contract (design 5.1, T22 freeze): every descriptor implements
/// <see cref="IRecordWriterDescriptor{T}"/>, its <c>Format(T, ref RecordWriter)</c> writes each field straight into
/// the <see cref="RecordWriter"/> (the converter loop of the freeze, quoting and padding in place), and the legacy
/// <c>Format(T, IBufferWriter&lt;char&gt;)</c> is the one-line adapter. The 4.9 write helpers, the stack buffer and
/// the pool are gone. What the emitted code writes at run time is checked by the Compat49 round trips.
/// </summary>
[Category("Generator")]
public sealed class WriterEmissionTests
{
    /// <summary>A fixed-length record: padding, alignment, overflow, a nullable, a converter with arguments, a line break.</summary>
    internal const string FixedShape = """
        [FixedLengthRecord(FixedMode.AllowVariableLength)] public class FixedShape
        {
            [FieldFixedLength(6), FieldAlign(AlignMode.Right, '0')] public int Id;
            [FieldFixedLength(10)] public string Name = "";
            [FieldFixedLength(8), FieldConverter(ConverterKind.Date, "yyyyMMdd")] public DateTime Day;
            [FieldFixedLength(5, OverflowMode = OverflowMode.Error)] public string Code = "";
            [FieldFixedLength(7), FieldAlign(AlignMode.Center, '*')] public decimal? Amount;
            [FieldFixedLength(3), FieldInNewLine] public string Tail = "";
        }
        """;

    private const string Usings = """
        using System;
        using ModernLibs.FileHelpers;
        using ModernLibs.FileHelpers.Converters;

        """;

    private const string RecordWriter = "global::ModernLibs.FileHelpers.RecordWriter";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static IEnumerable<string> AllShapes() =>
        [nameof(TokenizedEmissionTests.PlainShape), nameof(TokenizedEmissionTests.QuotedShape), nameof(TokenizedEmissionTests.RecyclableShape), nameof(FixedShape)];

    private static string Shape(string name) => name switch
    {
        nameof(TokenizedEmissionTests.PlainShape) => TokenizedEmissionTests.PlainShape,
        nameof(TokenizedEmissionTests.QuotedShape) => TokenizedEmissionTests.QuotedShape,
        nameof(TokenizedEmissionTests.RecyclableShape) => TokenizedEmissionTests.RecyclableShape,
        _ => FixedShape,
    };

    /// <summary>
    /// Generates <paramref name="shape"/> and returns the text with <c>\n</c> line ends; fails on compile errors or
    /// warnings.
    /// </summary>
    private static async Task<string> Generate(string shape)
    {
        var run = GeneratorHarness.RunGenerator(Usings + shape);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText + "\n" + run.GeneratedText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue().Because(string.Join("\n", run.GeneratedWarnings));
        return run.GeneratedText.ReplaceLineEndings("\n");
    }

    /// <summary>The method that starts at <paramref name="signature"/>, up to its closing brace (four spaces of indent), or null.</summary>
    private static string? Method(string text, string signature)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
            return null;
        int end = text.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        return text[start..(end + "\n    }\n".Length)];
    }

    private static string WriterFormat(string text, string type) =>
        Method(text, $"public void Format(global::{type} record, ref {RecordWriter} writer)")
        ?? throw new InvalidOperationException($"No Format(T, ref RecordWriter) for {type}:\n{text}");

    [Test]
    [MethodDataSource(nameof(AllShapes))]
    public async Task No_Copied_Write_Helpers_And_No_Stackalloc(string shape)
    {
        string text = await Generate(Shape(shape));

        var found = new List<string>();
        foreach (string banned in (string[])["__WriteText", "__WriteQuoted", "__WritePadded", "__Format<", "stackalloc", "ArrayPool", "WriteFieldNewLine"])
        {
            if (text.Contains(banned, StringComparison.Ordinal))
                found.Add(banned);
        }

        await Assert.That(found).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(AllShapes))]
    public async Task Legacy_Format_Is_The_One_Line_Adapter(string shape)
    {
        string text = await Generate(Shape(shape));
        string signature = $"public void Format(global::{shape} record, global::System.Buffers.IBufferWriter<char> writer)";

        await Assert.That(Method(text, signature)).IsEqualTo(
            signature + "\n"
            + "    {\n"
            + "        global::ModernLibs.FileHelpers.RecordFormatting.Format(this, record, writer);\n"
            + "    }\n");
        await Assert.That(Regex.Count(text, @"public void Format\(", RegexOptions.None, Timeout)).IsEqualTo(2);
    }

    /// <summary>
    /// Delimited, fixed-length, <c>[FieldInNewLine]</c>, value-type, constructor-bound and positional records: each
    /// descriptor lists <see cref="IRecordWriterDescriptor{T}"/> after its other interfaces and has the new
    /// <c>Format</c>; the generated descriptors of this assembly implement it at run time.
    /// </summary>
    [Test]
    public async Task Every_Record_Kind_Implements_IRecordWriterDescriptor()
    {
        const string shapes = """
            [DelimitedRecord(",")] public class Settable { public int A { get; set; } public string B { get; set; } = ""; }
            [DelimitedRecord(",")] public struct Struct { public int A; public string B; }
            [DelimitedRecord(",")] public class Ctor { public Ctor(int a, string b) { A = a; B = b; } public int A { get; } public string B { get; } }
            [DelimitedRecord(",")] public record Positional(int A, string B);
            [DelimitedRecord(",")] public class NewLine { public string A = ""; [FieldInNewLine] public int B; }
            [FixedLengthRecord] public class Fixed { [FieldFixedLength(3)] public string A = ""; [FieldFixedLength(2)] public int B; }
            [FixedLengthRecord] public readonly struct FixedStruct { public FixedStruct(string a) { A = a; } [FieldFixedLength(3)] public string A { get; } }
            """;
        var run = GeneratorHarness.RunGenerator(Usings + shapes);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue().Because(string.Join("\n", run.GeneratedWarnings));

        var trees = run.Result.GeneratedTrees.Select(t => t.GetText().ToString().ReplaceLineEndings("\n")).ToList();
        string[] types = ["Settable", "Struct", "Ctor", "Positional", "NewLine", "Fixed", "FixedStruct"];
        await Assert.That(trees.Count).IsEqualTo(types.Length);
        foreach (string type in types)
        {
            string tree = trees.Single(t => t.Contains($"class __{type}Descriptor ", StringComparison.Ordinal));
            string classLine = tree.Split('\n').Single(l => l.Contains($"class __{type}Descriptor ", StringComparison.Ordinal));
            await Assert.That(classLine).EndsWith($", global::ModernLibs.FileHelpers.IRecordWriterDescriptor<global::{type}>").Because(type);
            await Assert.That(tree).Contains($"public void Format(global::{type} record, ref {RecordWriter} writer)").Because(type);
        }

        await Assert.That(FileHelperRegistry<SimpleDelimitedRecord>.Descriptor is IRecordWriterDescriptor<SimpleDelimitedRecord>).IsTrue();
        await Assert.That(FileHelperRegistry<SimpleFixedLengthRecord>.Descriptor is IRecordWriterDescriptor<SimpleFixedLengthRecord>).IsTrue();
    }

    /// <summary>
    /// A <c>Nullable&lt;T&gt;</c> field formats only inside <c>if (value is { } v)</c>; its quoting (or padding) runs
    /// for a null value too, after the test, as in 4.9.
    /// </summary>
    [Test]
    public async Task Nullable_Field_Keeps_The_Null_Test()
    {
        const string shape = """
            [DelimitedRecord(",")] public class Nullables
            {
                public int? A;
                [FieldQuoted(QuoteMode.AlwaysQuoted)] public decimal? B;
            }
            """;
        string body = WriterFormat(await Generate(shape), "Nullables");

        await Assert.That(body).Contains(
            "        if (record.A is { } __nv0)\n"
            + "        {\n"
            + "            int __n0;\n"
            + "            while (!global::ModernLibs.FileHelpers.Converters.Int32Converter.Instance.TryFormat(__nv0, writer.FreeSpan, out __n0))\n"
            + "                writer.Grow(global::ModernLibs.FileHelpers.Converters.Int32Converter.Instance);\n"
            + "            writer.Advance(__n0);\n"
            + "        }\n");
        await Assert.That(body).Contains(
            "        int __s1 = writer.Position;\n"
            + "        if (record.B is { } __nv1)\n"
            + "        {\n"
            + "            int __n1;\n"
            + "            while (!global::ModernLibs.FileHelpers.Converters.DecimalConverter.Instance.TryFormat(__nv1, writer.FreeSpan, out __n1))\n"
            + "                writer.Grow(global::ModernLibs.FileHelpers.Converters.DecimalConverter.Instance);\n"
            + "            writer.Advance(__n1);\n"
            + "        }\n"
            + "        writer.Quote(__s1, '\"', true, true, \",\", \"B\");\n");
        await Assert.That(Regex.Count(body, @"is \{ \} __nv", RegexOptions.None, Timeout)).IsEqualTo(2);
    }

    [Test]
    public async Task FixedLength_Record_Reserves_Its_Width()
    {
        string body = WriterFormat(await Generate(FixedShape), "FixedShape");

        // 6 + 10 + 8 + 5 + 7 + 3 chars, the first statement of the body.
        await Assert.That(body).Contains($"ref {RecordWriter} writer)\n    {{\n        writer.Reserve(39);\n");
        await Assert.That(Regex.Count(body, @"writer\.Reserve\(", RegexOptions.None, Timeout)).IsEqualTo(1);

        string delimited = WriterFormat(await Generate(TokenizedEmissionTests.QuotedShape), "QuotedShape");
        await Assert.That(delimited).DoesNotContain("writer.Reserve(");
    }

    /// <summary>
    /// The fixed-length record: each field pads in place from its start; <c>[FieldAlign]</c>, <c>OverflowMode.Error</c>
    /// and the line break of <c>[FieldInNewLine]</c> keep the 4.9 arguments.
    /// </summary>
    [Test]
    public async Task FixedLength_Fields_Pad_In_Place()
    {
        string body = WriterFormat(await Generate(FixedShape), "FixedShape");

        await Assert.That(body).Contains("        writer.Pad(__s0, 6, 2, '0', false, \"Id\");\n");
        await Assert.That(body).Contains(
            "        int __s1 = writer.Position;\n"
            + "        writer.Write(global::System.MemoryExtensions.AsSpan(record.Name));\n"
            + "        writer.Pad(__s1, 10, 0, ' ', false, \"Name\");\n");
        await Assert.That(body).Contains("        writer.Pad(__s3, 5, 0, ' ', true, \"Code\");\n");
        await Assert.That(body).Contains("        writer.Pad(__s4, 7, 1, '*', false, \"Amount\");\n");
        await Assert.That(body).Contains(
            "        writer.WriteNewLine();\n"
            + "        int __s5 = writer.Position;\n");
        await Assert.That(Regex.Count(body, @"= writer\.Position;", RegexOptions.None, Timeout)).IsEqualTo(6);
        await Assert.That(body).Contains("writer.Grow(__Conv.__conv_field2);");
    }

    /// <summary>
    /// A delimited record reads <see cref="RecordWriter.Position"/> only for a quoted or an aligned field; a plain
    /// field is the converter loop or one <c>Write</c>, and each delimiter one <c>Write</c>.
    /// </summary>
    [Test]
    public async Task Delimited_Fields_Read_Position_Only_To_Quote_Or_Pad()
    {
        const string shape = """
            [DelimitedRecord(",")] public class Mixed
            {
                public int Id;
                [FieldQuoted(QuoteMode.OptionalForWrite)] public string Name = "";
                [FieldDelimiter("::")] public string Plain = "";
                [FieldFixedLength(4), FieldAlign(AlignMode.Left, '.')] public string Aligned = "";
                public long Last;
            }
            """;
        string body = WriterFormat(await Generate(shape), "Mixed");

        await Assert.That(body).IsEqualTo(
            $"public void Format(global::Mixed record, ref {RecordWriter} writer)\n"
            + "    {\n"
            + "        int __n0;\n"
            + "        while (!global::ModernLibs.FileHelpers.Converters.Int32Converter.Instance.TryFormat(record.Id, writer.FreeSpan, out __n0))\n"
            + "            writer.Grow(global::ModernLibs.FileHelpers.Converters.Int32Converter.Instance);\n"
            + "        writer.Advance(__n0);\n"
            + "        writer.Write(',');\n"
            + "        int __s1 = writer.Position;\n"
            + "        writer.Write(global::System.MemoryExtensions.AsSpan(record.Name));\n"
            + "        writer.Quote(__s1, '\"', false, true, \",\", \"Name\");\n"
            + "        writer.Write(',');\n"
            + "        writer.Write(global::System.MemoryExtensions.AsSpan(record.Plain));\n"
            + "        writer.Write(\"::\");\n"
            + "        int __s3 = writer.Position;\n"
            + "        writer.Write(global::System.MemoryExtensions.AsSpan(record.Aligned));\n"
            + "        writer.Pad(__s3, 4, 0, '.', false, \"Aligned\");\n"
            + "        writer.Write(',');\n"
            + "        int __n4;\n"
            + "        while (!global::ModernLibs.FileHelpers.Converters.Int64Converter.Instance.TryFormat(record.Last, writer.FreeSpan, out __n4))\n"
            + "            writer.Grow(global::ModernLibs.FileHelpers.Converters.Int64Converter.Instance);\n"
            + "        writer.Advance(__n4);\n"
            + "    }\n");
    }

    /// <summary>
    /// The custom converters of <c>RecyclableShape</c>: the cached converter field is the receiver and the argument
    /// of <c>Grow</c>; a <c>ConverterBase</c> value passes with <c>!</c>, as in 4.9.
    /// </summary>
    [Test]
    public async Task Custom_Converters_Use_The_Cached_Field()
    {
        string body = WriterFormat(await Generate(TokenizedEmissionTests.RecyclableShape), "RecyclableShape");

        await Assert.That(body).Contains(
            "        while (!__Conv.__conv_field0.TryFormat(record.Upper!, writer.FreeSpan, out __n0))\n"
            + "            writer.Grow(__Conv.__conv_field0);\n");
        await Assert.That(body).Contains("        while (!__Conv.__conv_field1.TryFormat(record.Price, writer.FreeSpan, out __n1))\n");
        await Assert.That(body).Contains("        if (record.MaybePrice is { } __nv2)\n");
        await Assert.That(body).Contains("            while (!__Conv.__conv_field2.TryFormat(__nv2, writer.FreeSpan, out __n2))\n");
        await Assert.That(body).DoesNotContain("writer.Position");
    }
}
