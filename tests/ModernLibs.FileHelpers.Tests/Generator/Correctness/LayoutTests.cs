// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Which members take part in the file layout and in which order.
/// </summary>
[Category("Generator")]
public sealed class LayoutTests
{
    [Test]
    [DisplayName("[FieldHidden] and [FieldNotInFile] members are neither read from nor written to the file")]
    public async Task HiddenFields_NotInFile_ReadAndWrite()
    {
        // Arrange: hidden members between mapped columns
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class H
            {
                public int A;
                [FieldHidden] public string Note = "n";
                [FieldNotInFile] public int Scratch;
                public int B;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<H>();
                    var r = engine.ReadString("1,2")[0];
                    r.Note = "changed"; r.Scratch = 99;
                    return r.A + "|" + r.B + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: two columns on read, two columns on write
        await Assert.That(output).IsEqualTo("1|2|1,2");
    }

    [Test]
    [DisplayName("[FieldValueDiscarded] consumes its column on read, is not stored, and its value is written (delimited)")]
    public async Task DiscardedField_Delimited_Symmetric()
    {
        // Arrange: discarded middle column
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class D
            {
                public int A;
                [FieldValueDiscarded] public string? B;
                public int C;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<D>();
                    var r = engine.ReadString("1,x,3")[0];
                    string read = r.A + "|" + (r.B ?? "null") + "|" + r.C;
                    r.B = "y";
                    return read + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: B not stored on read; all three columns written
        await Assert.That(output).IsEqualTo("1|null|3|1,y,3");
    }

    [Test]
    [DisplayName("[FieldValueDiscarded] fixed-length field keeps its width and its value is written")]
    public async Task DiscardedField_Fixed_Symmetric()
    {
        // Arrange: discarded fixed-length field
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class F
            {
                [FieldFixedLength(2)] public string A = "";
                [FieldFixedLength(3)] [FieldValueDiscarded] public string? B;
                [FieldFixedLength(2)] public string C = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<F>();
                    var r = engine.ReadString("aaXXXcc")[0];
                    string read = r.A + "|" + (r.B ?? "null") + "|" + r.C;
                    r.B = "yy";
                    return read + "|[" + engine.WriteString(new[] { r }).TrimEnd('\r', '\n') + "]";
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: B skipped on read, written padded to its width
        await Assert.That(output).IsEqualTo("aa|null|cc|[aayy cc]");
    }

    [Test]
    [DisplayName("Base-class fields come before derived-class fields (FileHelpers 3.x order)")]
    public async Task BaseClassFields_ComeFirst()
    {
        // Arrange: derived record without FieldOrder
        const string source = """
            using ModernLibs.FileHelpers;
            public class Base { public int Id; }
            [DelimitedRecord(",")]
            public sealed class Derived : Base { public string Name = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<Derived>();
                    var r = engine.ReadString("1,Bob")[0];
                    return r.Id + "|" + r.Name + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: column 1 is the base field
        await Assert.That(output).IsEqualTo("1|Bob|1,Bob");
    }

    [Test]
    [DisplayName("Missing trailing [FieldOptional] delimited fields read as empty/null instead of failing")]
    public async Task OptionalTrailingFields_Delimited_Descriptor()
    {
        // Arrange: two optional trailing fields
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class O
            {
                public int Id;
                public string Code = "";
                [FieldOptional] public string? Extra;
                [FieldOptional] public int? More;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var d = FileHelperRegistry<O>.Descriptor;
                    var r = d.Parse("1,A");
                    var fields = d.Fields;
                    return r.Id + "|" + r.Code + "|" + (r.Extra ?? "null") + "|" + (r.More is null) + "|" + fields[2].IsOptional + "|" + fields[1].IsOptional;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: optional fields empty; descriptor metadata flags them
        await Assert.That(output).IsEqualTo("1|A||True|True|False");
    }

    [Test]
    [DisplayName("Fixed-length line ending before trailing [FieldOptional] fields is accepted")]
    public async Task OptionalTrailingFields_Fixed_Engine()
    {
        // Arrange: exact-length record whose last field is optional
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class FO
            {
                [FieldFixedLength(3)] public string A = "";
                [FieldFixedLength(2)] public int B;
                [FieldFixedLength(4)] [FieldOptional] public string? C;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var rows = new FileHelperEngine<FO>().ReadString("abc12\nxyz34WXYZ");
                    return rows[0].A + rows[0].B + "[" + rows[0].C + "]|" + rows[1].A + rows[1].B + "[" + rows[1].C + "]";
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: both lines accepted
        await Assert.That(output).IsEqualTo("abc12[]|xyz34[WXYZ]");
    }

    [Test]
    [DisplayName("A non-optional field after a [FieldOptional] field reports MLFH018")]
    public async Task NonOptionalAfterOptional_ReportsDiagnostic()
    {
        // Arrange: optional field followed by a required one
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Bad
            {
                public int Id;
                [FieldOptional] public string? Extra;
                public string Last = "";
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH018 at the offending field
        var diagnostics = run.Diagnostics("MLFH018");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("Last");
    }
}
