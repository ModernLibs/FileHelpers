// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Nullable value types, enums, TimeSpan, DateTimeOffset, Guid, unsigned integers,
/// sbyte and char parse AND format in delimited and fixed-length records; unsupported types are
/// reported instead of silently defaulting.
/// </summary>
[Category("Generator")]
public sealed class TypeCoverageTests
{
    private const string Guid1 = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    [Test]
    [DisplayName("Delimited record reads and writes every supported scalar type")]
    public async Task Delimited_AllSupportedTypes_RoundTrip()
    {
        // Arrange: a record with one field per newly covered type and a probe that reads then writes
        const string source = """
            using ModernLibs.FileHelpers;
            public enum Color { Red, Green, Blue }
            [DelimitedRecord(",")]
            public sealed class Types
            {
                public int? NInt; public decimal? NDec; public System.DateTime? NDate; public bool? NBool;
                public System.Guid G; public System.Guid? NG; public uint U; public ulong UL; public ushort US; public sbyte SB;
                public char C; public char? NC; public Color E; public Color? NE; public System.TimeSpan TS; public System.DateTimeOffset DTO;
            }
            public static class Probe
            {
                public static string Run()
                {
                    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                    var engine = new FileHelperEngine<Types>();
                    var r = engine.ReadString("5,1.5,15012024,true,3f2504e0-4f89-11d3-9a0c-0305e82c3301,,4000000000,18000000000000000000,65000,-5,x,,green,,01:02:03,2024-01-15T10:30:00.0000000+02:00")[0];
                    string values = r.NInt + "|" + r.NDec + "|" + r.NDate?.Day + "|" + r.NBool + "|" + r.G + "|" + (r.NG is null) + "|"
                        + r.U + "|" + r.UL + "|" + r.US + "|" + r.SB + "|" + r.C + "|" + (r.NC is null) + "|" + r.E + "|" + (r.NE is null) + "|"
                        + r.TS.TotalSeconds + "|" + r.DTO.Offset.TotalHours;
                    return values + "\n" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: every value parsed correctly and the written line round-trips
        await Assert.That(output).IsEqualTo(
            "5|1.5|15|True|" + Guid1 + "|True|4000000000|18000000000000000000|65000|-5|x|True|Green|True|3723|2\n"
            + "5,1.5,15012024,True," + Guid1 + ",,4000000000,18000000000000000000,65000,-5,x,,Green,,01:02:03,2024-01-15T10:30:00.0000000+02:00");
    }

    [Test]
    [DisplayName("Fixed-length record writes and reads back every supported scalar type")]
    public async Task Fixed_AllSupportedTypes_RoundTrip()
    {
        // Arrange: fixed-length record with nullable, enum, Guid, unsigned, char and TimeSpan fields
        const string source = """
            using ModernLibs.FileHelpers;
            public enum Color { Red, Green, Blue }
            [FixedLengthRecord]
            public sealed class FixedTypes
            {
                [FieldFixedLength(5)] public int? NInt;
                [FieldFixedLength(6)] public decimal? NDec;
                [FieldFixedLength(36)] public System.Guid? NG;
                [FieldFixedLength(10)] public uint U;
                [FieldFixedLength(1)] public char C;
                [FieldFixedLength(6)] public Color E;
                [FieldFixedLength(6)] public Color? NE;
                [FieldFixedLength(8)] public System.TimeSpan TS;
                [FieldFixedLength(4)] public sbyte SB;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<FixedTypes>();
                    var record = new FixedTypes { NInt = 42, NDec = null, NG = System.Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), U = 7, C = 'z', E = Color.Blue, NE = null, TS = new System.TimeSpan(1, 2, 3), SB = -3 };
                    string line = engine.WriteString(new[] { record }).TrimEnd('\r', '\n');
                    var back = engine.ReadString(line)[0];
                    return "[" + line + "]\n" + back.NInt + "|" + (back.NDec is null) + "|" + back.NG + "|" + back.U + "|" + back.C + "|" + back.E + "|" + (back.NE is null) + "|" + back.TS + "|" + back.SB;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: padded line as expected and every value read back
        await Assert.That(output).IsEqualTo(
            "[42         " + Guid1 + "7         zBlue        01:02:03-3  ]\n"
            + "42|True|" + Guid1 + "|7|z|Blue|True|01:02:03|-3");
    }

    [Test]
    [DisplayName("Empty nullable fields read as null, not as zero")]
    public async Task EmptyNullableFields_ReadAsNull()
    {
        // Arrange: nullable fields with empty input
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class N { public int? A; public System.Guid? B; public char? C; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<N>().ReadString(",,")[0];
                    return (r.A is null) + "|" + (r.B is null) + "|" + (r.C is null);
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: all null
        await Assert.That(output).IsEqualTo("True|True|True");
    }

    [Test]
    [DisplayName("Enum fields parse by name case-insensitively and reject unknown names with ConvertException")]
    public async Task Enum_ParsesByName_RejectsUnknown()
    {
        // Arrange: enum field read with a lower-case name and with an unknown name
        const string source = """
            using ModernLibs.FileHelpers;
            public enum Kind { Alpha, Beta }
            [DelimitedRecord(",")]
            public sealed class E { public Kind K; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<E>();
                    string ok = engine.ReadString("beta")[0].K.ToString();
                    try { engine.ReadString("Gamma"); return ok + "|no error"; }
                    catch (ConvertException ex) { return ok + "|" + ex.FieldName; }
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: Beta parsed, Gamma rejected for field K
        await Assert.That(output).IsEqualTo("Beta|K");
    }

    [Test]
    [DisplayName("No generated code path emits default(T) // TODO")]
    public async Task Generated_NeverEmitsTodoDefault()
    {
        // Arrange: record with enum and TimeSpan (historic TODO paths) plus a ref-struct-free struct
        const string source = """
            using ModernLibs.FileHelpers;
            public enum Kind { Alpha, Beta }
            [DelimitedRecord(",")] public sealed class A { public Kind K; public System.TimeSpan T; public System.DateTimeOffset O; }
            [FixedLengthRecord] public struct B { [FieldFixedLength(5)] public Kind K; [FieldFixedLength(8)] public System.TimeSpan T; }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: no TODO placeholders and the output compiles
        await Assert.That(run.GeneratedText).DoesNotContain("TODO");
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("A field type without a converter reports MLFH013 instead of compiling to default(T)")]
    public async Task UnsupportedType_ReportsDiagnostic()
    {
        // Arrange: a field of an arbitrary class type
        const string source = """
            using ModernLibs.FileHelpers;
            public sealed class Payload { }
            [DelimitedRecord(",")] public sealed class U { public string Name = ""; public Payload? Data; }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH013 points at the field and the descriptor is not emitted
        var diagnostics = run.Diagnostics("MLFH013");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Unresolved field and converter types report only the compiler error, no MLFH013/MLFH004")]
    public async Task UnresolvedTypes_NoFileHelpersDiagnostics()
    {
        // Arrange: Missing, Missing? and List<Missing> fields and a missing converter type (CS0246)
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class U
            {
                public string Name = "";
                public Missing Data = null!;
                public Missing2? Other;
                public System.Collections.Generic.List<Missing3> Many = null!;
                [FieldConverter(typeof(MissingConverter))] public string Converted = "";
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: the compiler reports CS0246, FileHelpers adds nothing and emits no descriptor code
        await Assert.That(run.CompileErrors.Count(static d => string.Equals(d.Id, "CS0246", StringComparison.Ordinal))).IsEqualTo(4);
        await Assert.That(run.CompileErrors.All(static d => string.Equals(d.Id, "CS0246", StringComparison.Ordinal))).IsTrue().Because(run.ErrorText);
        await Assert.That(run.Diagnostics("MLFH013").Count).IsEqualTo(0);
        await Assert.That(run.Diagnostics("MLFH004").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(0);
    }
}
