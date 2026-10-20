// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Globalization;
using Microsoft.CodeAnalysis;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// VG4: FileHelpers 3.x maps non-public instance fields. The generated descriptor lives in the same
/// assembly, so <c>internal</c> and <c>protected internal</c> members are mapped like public ones;
/// <c>private</c>, <c>protected</c> and <c>private protected</c> members cannot be accessed: annotated
/// ones report MLFH023, unannotated ones are not part of the layout (MLFH028 when 3.x would map them).
/// </summary>
[Category("Generator")]
public sealed class MemberAccessibilityTests
{
    private static readonly string[] ExpectedCacheWarning =
    [
        "Member '_cache' in type 'R' is private, so it is not part of the record layout, but FileHelpers 3.x maps it as a column. How to fix: Make the member public or internal (with an accessible get accessor) to map it, or mark it [FieldHidden] if it is not a column.",
    ];

    private static readonly string[] ExpectedLegacyMembers =
    [
        "Member '_baseField' in type 'R'", "Member '_a' in type 'R'", "Member 'P' in type 'R'", "Member '_pp' in type 'R'",
        "Member 'Auto' in type 'R'", "Member 'AutoProtected' in type 'R'", "Member 'PrivateGetter' in type 'R'",
    ];

    [Test]
    [DisplayName("VG4: an internal field is part of the fixed-length layout")]
    public async Task InternalField_IsMapped()
    {
        // Arrange: public A, internal B, public C (3 chars each)
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class R
            {
                [FieldFixedLength(3)] public string A = "";
                [FieldFixedLength(3)] internal string B = "";
                [FieldFixedLength(3)] public string C = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var r = engine.ReadString("aaabbbccc")[0];
                    return r.A + "|" + r.B + "|" + r.C + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("aaa|bbb|ccc|aaabbbccc");
    }

    [Test]
    [DisplayName("VG4: internal and protected internal properties are mapped")]
    public async Task InternalProperties_AreMapped()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public class R
            {
                internal int Id { get; set; }
                protected internal string Name { get; set; } = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("7,x")[0];
                    return r.Id + "|" + r.Name;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("7|x");
    }

    [Test]
    [DisplayName("VG4: an annotated private member reports MLFH023 at the member")]
    public async Task AnnotatedPrivateMember_ReportsMlfh023()
    {
        // Arrange: private B carries [FieldFixedLength] (line 6, zero-based 5); protected D carries [FieldOrder]
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public class R
            {
                [FieldFixedLength(3)] public string A = "";
                [FieldFixedLength(3)] private string B = "";
                [FieldFixedLength(3)] protected string D = "";
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: two errors, no descriptor
        var diagnostics = run.Diagnostics("MLFH023");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        await Assert.That(diagnostics.Any(static d => d.Location.GetLineSpan().StartLinePosition.Line == 5)).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }

    [Test]
    [DisplayName("VG4: unannotated or hidden private members are not mapped and get no MLFH023")]
    public async Task UnannotatedPrivateMember_IsSkipped()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                public string A = "";
                private int _cache;
                [FieldHidden] private int _hidden;
                public string B = "";
                public int Use() => _cache + _hidden;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("x,y")[0];
                    return r.A + "|" + r.B;
                }
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert: _cache gets the MLFH028 warning (3.x maps it), the hidden one nothing
        await Assert.That(run.Diagnostics("MLFH023").Count).IsEqualTo(0);
        await Assert.That(run.Diagnostics("MLFH028").Select(static d => d.GetMessage(CultureInfo.InvariantCulture)))
            .IsEquivalentTo(ExpectedCacheWarning);
        await Assert.That(output).IsEqualTo("x|y");
    }

    [Test]
    [DisplayName("Unannotated private, protected and private protected fields and auto-properties report MLFH028 (3.x maps them)")]
    public async Task UnannotatedNonPublicMembers_ReportMlfh028()
    {
        // Arrange: FileHelpers 3.x maps every instance field, including auto-property backing fields
        const string source = """
            using ModernLibs.FileHelpers;
            public class Base { private int _baseField; public int UseBase() => _baseField; }
            [DelimitedRecord(",")]
            public class R : Base
            {
                public string A = "";
                private int _a;
                protected string P = "";
                private protected readonly int _pp;
                private string Auto { get; set; } = "";
                protected int AutoProtected { get; set; }
                public string PrivateGetter { private get; set; } = "";
                public int Use() => _a + _pp + Auto.Length + PrivateGetter.Length;
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: one warning per member, at the member; the descriptor is still generated
        var diagnostics = run.Diagnostics("MLFH028");
        await Assert.That(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture).Split(" is ")[0]))
            .IsEquivalentTo(ExpectedLegacyMembers);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Warning && d.Location.IsInSource)).IsTrue();
        await Assert.That(diagnostics.Single(static d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'PrivateGetter'", StringComparison.Ordinal)).GetMessage(CultureInfo.InvariantCulture))
            .Contains("is readable only through a private get accessor");
        await Assert.That(diagnostics.Single(static d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'_pp'", StringComparison.Ordinal)).GetMessage(CultureInfo.InvariantCulture))
            .Contains("is private protected");
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("MLFH028 is not reported for members 3.x does not map or that are excluded from the layout")]
    public async Task ExcludedMembers_NoMlfh028()
    {
        // Arrange: consts, statics, hidden members, delegates/events, computed properties, compiler-generated members
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public class R(int seed)
            {
                public R() : this(0) { }
                public string A = "";
                internal int B;
                public int Auto { get; set; }
                internal string InternalAuto { get; set; } = "";
                private const int Const = 1;
                private static int s_static;
                private static int StaticAuto { get; set; }
                [FieldHidden] private int _hidden;
                [FieldNotInFile] private string _notInFile = "";
                [FieldHidden] private string HiddenAuto { get; set; } = "";
                [field: FieldHidden] private string HiddenBacking { get; set; } = "";
                private System.Action? _callback;
                private event System.EventHandler? Changed;
                private int Computed => Const + seed;
                public int Use() => s_static + StaticAuto + _hidden + _notInFile.Length + HiddenAuto.Length + HiddenBacking.Length + Computed + (_callback is null ? 0 : 1) + (Changed is null ? 0 : 1);
            }
            [DelimitedRecord(",")] public record P(int X, string Y);
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await Assert.That(run.Diagnostics("MLFH028").Count).IsEqualTo(0).Because(string.Join("\n", run.Diagnostics("MLFH028")));
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("An annotated property without an accessible getter reports MLFH023")]
    public async Task AnnotatedPropertyWithoutGetter_ReportsMlfh023()
    {
        // Arrange: a write-only property and a property with a private getter, both annotated
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public class R
            {
                [FieldFixedLength(3)] public string A = "";
                [FieldFixedLength(3)] public string WriteOnly { set { } }
                [FieldFixedLength(3)] public string PrivateGet { private get; set; } = "";
                public int Use() => PrivateGet.Length;
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: one error per property, at the property; no descriptor (the declared layout cannot be honoured)
        var messages = run.Diagnostics("MLFH023").Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).ToList();
        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages.Any(static m => m.Contains("'WriteOnly' in type 'R' has FileHelpers field attributes but is write-only", StringComparison.Ordinal))).IsTrue();
        await Assert.That(messages.Any(static m => m.Contains("'PrivateGet' in type 'R' has FileHelpers field attributes but is readable only through a private get accessor", StringComparison.Ordinal))).IsTrue();
        await Assert.That(run.Diagnostics("MLFH023").All(static d => d.Location.IsInSource)).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }
}
