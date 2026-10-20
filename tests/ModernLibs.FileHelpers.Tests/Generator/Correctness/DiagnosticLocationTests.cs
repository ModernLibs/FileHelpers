// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Diagnostics carry real source locations, MLFH001 matches by full metadata name,
/// keyword identifiers are escaped, and the MLFH007 threshold is configurable as documented.
/// </summary>
[Category("Generator")]
public sealed class DiagnosticLocationTests
{
    [Test]
    [DisplayName("Field-level diagnostics point at the member declaration")]
    public async Task FieldDiagnostic_HasMemberLocation()
    {
        // Arrange: duplicate FieldOrder on the second member (line 6, zero-based 5)
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Dup
            {
                [FieldOrder(1)] public string First = "";
                [FieldOrder(1)] public string Second = "";
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH003 located in the consumer file on the Second member's line
        var diagnostic = run.Diagnostics("MLFH003").Single();
        await Assert.That(diagnostic.Location.IsInSource).IsTrue();
        await Assert.That(diagnostic.Location.SourceTree!.FilePath).IsEqualTo("Consumer.cs");
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(5);
    }

    [Test]
    [DisplayName("Record-level diagnostics point at the record attribute")]
    public async Task RecordDiagnostic_HasAttributeLocation()
    {
        // Arrange: record without public fields (MLFH009) — attribute on line 2 (zero-based 1)
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Empty { }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH009 located on the attribute line
        var diagnostic = run.Diagnostics("MLFH009").Single();
        await Assert.That(diagnostic.Location.IsInSource).IsTrue();
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(1);
    }

    [Test]
    [DisplayName("MLFH001 is located at the type argument of the engine usage")]
    public async Task Mlfh001_HasUsageLocation()
    {
        // Arrange: undecorated type used with FileHelperEngine on line 5 (zero-based 4)
        const string source = """
            using ModernLibs.FileHelpers;
            public sealed class Undecorated { public string Name = ""; }
            public static class Use
            {
                public static object Make() => new FileHelperEngine<Undecorated>();
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: located on the usage line
        var diagnostic = run.Diagnostics("MLFH001").Single();
        await Assert.That(diagnostic.Location.IsInSource).IsTrue();
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(4);
    }

    [Test]
    [DisplayName("MLFH001 does not fire for a FileHelperEngine<T> type from another namespace (3.x migration)")]
    public async Task Mlfh001_IgnoresForeignEngineType()
    {
        // Arrange: a user-defined FileHelpers.FileHelperEngine<T> (stands in for the original 3.x library)
        const string source = """
            namespace FileHelpers
            {
                public sealed class FileHelperEngine<T> { }
                public sealed class FileHelperRegistry<T> { }
            }
            public sealed class Legacy { public string Name = ""; }
            public static class Use
            {
                public static object Make() => new FileHelpers.FileHelperEngine<Legacy>();
                public static object Reg() => new FileHelpers.FileHelperRegistry<Legacy>();
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: no MLFH001
        await Assert.That(run.Diagnostics("MLFH001").Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Members named with C# keywords are escaped in generated code")]
    public async Task KeywordMemberNames_AreEscaped()
    {
        // Arrange: members named class / int / event
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class K
            {
                public string @class = "";
                public int @int;
                public string @event { get; set; } = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<K>();
                    var r = engine.ReadString("a,1,b")[0];
                    return r.@class + r.@int + r.@event + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: round trip works
        await Assert.That(output).IsEqualTo("a1b|a,1,b");
    }

    [Test]
    [DisplayName("MLFH007 threshold is read from the FileHelpersMaxFieldWidth build property")]
    public async Task Mlfh007_ThresholdFromBuildProperty()
    {
        // Arrange: a 10-wide field and a threshold of 5
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class W { [FieldFixedLength(10)] public string Name = ""; }
            """;
        var options = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.FileHelpersMaxFieldWidth"] = "5",
        };

        // Act: generate with and without the property
        var configured = GeneratorHarness.RunGenerator(source, options);
        var defaults = GeneratorHarness.RunGenerator(source);

        // Assert: only the configured run warns
        await Assert.That(configured.Diagnostics("MLFH007").Count).IsEqualTo(1);
        await Assert.That(defaults.Diagnostics("MLFH007").Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("MLFH007 threshold is read from the documented filehelpers.max_field_width global config key")]
    public async Task Mlfh007_ThresholdFromGlobalConfig()
    {
        // Arrange: a 10-wide field and a .globalconfig threshold of 5
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class W { [FieldFixedLength(10)] public string Name = ""; }
            """;
        var options = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["filehelpers.max_field_width"] = "5",
        };

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source, options);

        // Assert: warning reported
        await Assert.That(run.Diagnostics("MLFH007").Count).IsEqualTo(1);
    }
}
