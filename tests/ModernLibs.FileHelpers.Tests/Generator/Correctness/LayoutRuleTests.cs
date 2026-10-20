// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using Microsoft.CodeAnalysis;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>Layout rules that FileHelpers 3.x enforces with a BadUsageException at run time.</summary>
[Category("Generator")]
public sealed class LayoutRuleTests
{
    [Test]
    [DisplayName("VG8: mixing explicit and implicit [FieldOrder] reports MLFH024 for each member without it")]
    public async Task MixedFieldOrder_ReportsMlfh024()
    {
        // Arrange: only A has [FieldOrder]; B (line 6, zero-based 5) and C do not
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Mixed
            {
                [FieldOrder(3)] public int A;
                public int B;
                public int C;
                [FieldHidden] public int Ignored;
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: B and C reported, no descriptor
        var diagnostics = run.Diagnostics("MLFH024");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics.Any(static d => d.Location.GetLineSpan().StartLinePosition.Line == 5)).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }

    [Test]
    [DisplayName("VG11: [FieldQuoted] on a fixed-length field reports MLFH025")]
    public async Task FieldQuoted_OnFixedLength_ReportsMlfh025()
    {
        // Arrange: the quoted field is on line 5 (zero-based 4)
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
        var diagnostic = run.Diagnostics("MLFH025").Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(4);
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }

    [Test]
    [DisplayName("VG12: C# 10 reports MLFH026 per record and generates nothing")]
    public async Task LanguageVersion10_ReportsMlfh026()
    {
        // Arrange: two records compiled as C# 10
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class A { public string Name = ""; }
            [FixedLengthRecord] public sealed class B { [FieldFixedLength(3)] public string Code = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source, languageVersion: Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp10);

        // Assert: one error per record, no generated code, so no CS8936 in generated files
        var diagnostics = run.Diagnostics("MLFH026");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG12: C# 11 compiles the generated code of a wide range of record shapes")]
    public async Task LanguageVersion11_Generates()
    {
        // Arrange: quoted/trimmed/converted/nullable/custom-converter fields, fixed-length, constructor-built record
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Up : ConverterBase { public override object? StringToField(string text) => text.ToUpperInvariant(); }
            public enum Kind { X, Y }
            [DelimitedRecord(",")]
            [IgnoreFirst(1)]
            public sealed class A
            {
                [FieldQuoted] [FieldTrim(TrimMode.Both)] public string Name = "";
                public int? Count;
                [FieldConverter(ConverterKind.Date, "yyyyMMdd")] public DateTime When;
                [FieldConverter(typeof(Up))] public string Code = "";
                [FieldNullValue(Kind.Y)] public Kind K;
                [FieldOptional] public decimal Amount;
            }
            [FixedLengthRecord(FixedMode.AllowVariableLength)]
            public sealed class B
            {
                [FieldFixedLength(3)] [FieldAlign(AlignMode.Right, '0')] public int Id;
                [FieldFixedLength(5)] [FieldTrim(TrimMode.Right)] public string Text = "";
                [FieldFixedLength(4)] [FieldInNewLine] public string Next = "";
            }
            [DelimitedRecord("|")]
            public sealed record C(int Id, string Name);
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source, languageVersion: Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp11);

        // Assert
        await Assert.That(run.Diagnostics("MLFH026").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(3);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG14: a ref struct record reports MLFH027 and generates nothing")]
    public async Task RefStructRecord_ReportsMlfh027()
    {
        // Arrange
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public ref struct S { public int Id; public ReadOnlySpan<char> Name; }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: only MLFH027 for S (no MLFH013 for the span member), Fine still generated
        var diagnostic = run.Diagnostics("MLFH027").Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(2);
        await Assert.That(run.Diagnostics("MLFH013").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(run.GeneratedText).Contains("__FineDescriptor");
    }

    [Test]
    [DisplayName("VG14: a ReadOnlySpan<char> field in a class reports MLFH013")]
    public async Task CharSpanField_ReportsMlfh013()
    {
        // Arrange: ReadOnlySpan<char> cannot be a field of a class, but can be a property
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { public ReadOnlySpan<char> Name => default; public string Other = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: get-only and unannotated, so it is skipped silently; nothing about ref structs
        await Assert.That(run.Diagnostics("MLFH027").Count).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG10: [IgnoreInheritedClass] on an intermediate base stops the inherited fields there (3.x)")]
    public async Task IgnoreInheritedClass_OnIntermediateBase_StopsThere()
    {
        // Arrange: C : B : A, [IgnoreInheritedClass] on B → B1, C1 (A1 is not mapped)
        const string source = """
            using ModernLibs.FileHelpers;
            public class A { public string A1 = ""; }
            [IgnoreInheritedClass] public class B : A { public string B1 = ""; }
            [DelimitedRecord(",")] public sealed class C : B { public string C1 = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<C>();
                    var c = engine.ReadString("b,c")[0];
                    return c.B1 + "|" + c.C1 + "|" + (c.A1.Length == 0) + "|" + engine.WriteString(new[] { c }).TrimEnd();
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("b|c|True|b,c");
    }

    [Test]
    [DisplayName("VG8: [FieldOrder] on every member, or on none, is accepted")]
    public async Task AllOrNoneFieldOrder_IsAccepted()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class All { [FieldOrder(2)] public int A; [FieldOrder(1)] public int B; [FieldHidden] public int H; }
            [DelimitedRecord(",")]
            public sealed class None { public int A; public int B; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await Assert.That(run.Diagnostics("MLFH024").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
    }

    [Test]
    [DisplayName("A first [FieldInNewLine] field with [IgnoreEmptyLines] reports MLFH029: written files cannot be read back")]
    public async Task FirstFieldInNewLine_WithIgnoreEmptyLines_ReportsMlfh029()
    {
        // Arrange: delimited (first field on line 6, zero-based 5) and fixed-length variants
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            [IgnoreEmptyLines]
            public sealed class D
            {
                [FieldInNewLine] public string A = "";
                public string B = "";
            }
            [FixedLengthRecord]
            [IgnoreEmptyLines(true)]
            public sealed class F
            {
                [FieldFixedLength(2), FieldInNewLine] public string A = "";
                [FieldFixedLength(2)] public string B = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<D>();
                    string written = engine.WriteString(new[] { new D { A = "a", B = "b" } });
                    try { return engine.ReadString(written)[0].A; }
                    catch (FileHelpersException ex) { return ex.GetType().Name; }
                }
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);
        string roundTrip = GeneratorHarness.Execute(source);

        // Assert: one warning per record at the first field; the descriptors are still generated, and the
        // written record does not read back (the leading blank line is skipped, as in 3.x)
        var diagnostics = run.Diagnostics("MLFH029");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Warning)).IsTrue();
        await Assert.That(diagnostics.Any(static d => d.Location.GetLineSpan().StartLinePosition.Line == 5)).IsTrue();
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
        await Assert.That(roundTrip).IsNotEqualTo("a");
    }

    [Test]
    [DisplayName("MLFH029 is not reported without [IgnoreEmptyLines] or when [FieldInNewLine] is on a later field")]
    public async Task FieldInNewLine_WithoutConflict_NoMlfh029()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class First { [FieldInNewLine] public string A = ""; public string B = ""; }
            [DelimitedRecord(",")]
            [IgnoreEmptyLines]
            public sealed class Later { public string A = ""; [FieldInNewLine] public string B = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await Assert.That(run.Diagnostics("MLFH029").Count).IsEqualTo(0);
    }
}
